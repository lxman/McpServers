using System.Collections.Concurrent;
using DebugServer.Core.Models;
using Microsoft.Extensions.Logging;

namespace DebugServer.Core.Services;

/// <summary>
/// Manages multiple debugging sessions and tracks their state.
/// Simplified to work with the redesigned MiClient.
/// </summary>
public class DebuggerSessionManager
{
    private readonly ILogger<DebuggerSessionManager> _logger;
    private readonly MiClient _miClient;
    private readonly ConcurrentDictionary<string, DebugSession> _sessions = new();

    public DebuggerSessionManager(ILogger<DebuggerSessionManager> logger, MiClient miClient)
    {
        _logger = logger;
        _miClient = miClient;

        // Subscribe to MiClient events to track the session state
        _miClient.AsyncEventReceived += OnAsyncEventReceived;
        _miClient.SessionDisconnected += OnSessionDisconnected;
    }

    /// <summary>
    /// Register a new debug session.
    /// Called after MiClient.LaunchAsync() succeeds.
    /// </summary>
    public void RegisterSession(DebugSession session)
    {
        _sessions[session.SessionId] = session;
        _logger.LogInformation("Registered debug session {SessionId} for {Executable}", 
            session.SessionId, session.ExecutablePath);
    }

    /// <summary>
    /// Get a session by ID.
    /// </summary>
    public DebugSession? GetSession(string sessionId)
    {
        _sessions.TryGetValue(sessionId, out DebugSession? session);
        return session;
    }

    /// <summary>
    /// Get all registered sessions.
    /// </summary>
    public IEnumerable<DebugSession> GetAllSessions()
    {
        return _sessions.Values;
    }

    /// <summary>
    /// Remove a session from tracking.
    /// </summary>
    public bool RemoveSession(string sessionId)
    {
        if (!_sessions.TryRemove(sessionId, out DebugSession? session)) 
            return false;

        _logger.LogInformation("Removed debug session {SessionId}", sessionId);
        return true;
    }

    /// <summary>
    /// Update the session state when async events are received.
    /// </summary>
    private void OnAsyncEventReceived(object? sender, MiAsyncEventArgs e)
    {
        if (!_sessions.TryGetValue(e.SessionId, out DebugSession? session))
            return;

        switch (e.EventType)
        {
            case "stopped":
                // Program stopped at breakpoint, exception, or step
                session.State = DebugSessionState.Stopped;
                
                // Update breakpoint hit count if stopped at breakpoint
                if (e.ParsedData.TryGetValue("bkptno", out string? bkptNo) &&
                    int.TryParse(bkptNo, out int breakpointId))
                {
                    Breakpoint? breakpoint = session.Breakpoints.FirstOrDefault(b => b.Id == breakpointId);
                    if (breakpoint != null)
                    {
                        breakpoint.HitCount++;
                        _logger.LogDebug("Breakpoint {BreakpointId} hit (count: {HitCount})", 
                            breakpointId, breakpoint.HitCount);
                    }
                }

                // Check stop reason
                if (e.ParsedData.TryGetValue("reason", out string? reason))
                {
                    _logger.LogInformation("Session {SessionId} stopped: {Reason}", 
                        e.SessionId, reason);
                    
                    if (reason == "exited" || reason == "exited-normally")
                    {
                        session.State = DebugSessionState.Exited;
                    }
                }
                break;

            case "running":
                // Program resumed execution
                session.State = DebugSessionState.Running;
                _logger.LogDebug("Session {SessionId} running", e.SessionId);
                break;

            case "library-loaded":
                // Library/assembly loaded - could track these if needed
                _logger.LogTrace("Library loaded in session {SessionId}", e.SessionId);
                break;

            case "thread-created":
                // Thread created - could track these if needed
                _logger.LogTrace("Thread created in session {SessionId}", e.SessionId);
                break;

            case "breakpoint-modified":
                // Breakpoint was resolved or modified
                if (e.ParsedData.TryGetValue("number", out string? bpNum) &&
                    int.TryParse(bpNum, out int bpId))
                {
                    Breakpoint? breakpoint = session.Breakpoints.FirstOrDefault(b => b.Id == bpId);
                    if (breakpoint != null)
                    {
                        breakpoint.Verified = true;
                        _logger.LogDebug("Breakpoint {BreakpointId} verified", bpId);
                    }
                }
                break;
        }
    }

    /// <summary>
    /// Update session state when session disconnects.
    /// </summary>
    private void OnSessionDisconnected(object? sender, SessionDisconnectedEventArgs e)
    {
        if (!_sessions.TryGetValue(e.SessionId, out DebugSession? session))
            return;

        session.State = e.IsExpected 
            ? DebugSessionState.Terminated 
            : DebugSessionState.Error;

        _logger.LogInformation("Session {SessionId} disconnected: {Reason}", 
            e.SessionId, e.Reason);
    }
}