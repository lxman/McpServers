using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;
using DebugServer.Core.Models;
using Microsoft.Extensions.Logging;

namespace DebugServer.Core.Services;

/// <summary>
/// MI (Machine Interface) Protocol Client for netcoredbg.
/// Implements asynchronous command-response matching with a background reader pattern.
/// </summary>
public class MiClient(ILogger<MiClient> logger) : IDisposable
{
    // Process management per session
    private readonly Dictionary<string, Process> _debuggerProcesses = new();
    private readonly Dictionary<string, StreamWriter> _inputStreams = new();
    
    // Background reader infrastructure
    private readonly Dictionary<string, Task> _outputReaders = new();
    private readonly Dictionary<string, Task> _outputProcessors = new();
    private readonly Dictionary<string, BlockingCollection<string>> _outputQueues = new();
    private readonly Dictionary<string, CancellationTokenSource> _readerCancellations = new();
    
    // Command-response matching
    private readonly Dictionary<string, ConcurrentDictionary<int, PendingCommand>> _pendingCommands = new();
    private readonly SemaphoreSlim _commandLock = new(1, 1);
    private int _nextToken = 1;
    
    // Constants
    private const int MaxQueueSize = 10000;
    private const int DefaultTimeoutSeconds = 30;
    
    // Events
    public event EventHandler<MiAsyncEventArgs>? AsyncEventReceived;
    public event EventHandler<ConsoleOutputEventArgs>? ConsoleOutputReceived;
    public event EventHandler<SessionDisconnectedEventArgs>? SessionDisconnected;

    /// <summary>
    /// Launch a new debug session with netcoredbg.
    /// </summary>
    public async Task<string> LaunchAsync(
        string executablePath,
        string? workingDirectory = null,
        string[]? arguments = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Launching debug session for {ExecutablePath}", executablePath);

        // Generate session ID
        var sessionId = Guid.NewGuid().ToString();

        try
        {
            // Determine netcoredbg path
            var netcoredbgPath = FindNetcoredbg();
            if (string.IsNullOrEmpty(netcoredbgPath))
            {
                throw new FileNotFoundException("netcoredbg.exe not found in PATH");
            }

            // Build command line: netcoredbg.exe --interpreter=mi -- dotnet <dll> [args]
            var commandLine = BuildCommandLine(executablePath, arguments);

            // Configure process
            var startInfo = new ProcessStartInfo
            {
                FileName = netcoredbgPath,
                Arguments = commandLine,
                WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(executablePath) ?? Environment.CurrentDirectory,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            // Start process
            var process = new Process { StartInfo = startInfo };
            process.Start();

            logger.LogInformation("Started netcoredbg process (PID: {ProcessId}) for session {SessionId}", 
                process.Id, sessionId);

            // Store process and streams
            _debuggerProcesses[sessionId] = process;
            _inputStreams[sessionId] = process.StandardInput;

            // Initialize command tracking
            _pendingCommands[sessionId] = new ConcurrentDictionary<int, PendingCommand>();

            // Initialize output queue
            _outputQueues[sessionId] = new BlockingCollection<string>(MaxQueueSize);

            // Start the background reader and processor
            var cts = new CancellationTokenSource();
            _readerCancellations[sessionId] = cts;

            _outputReaders[sessionId] = StartBackgroundReaderAsync(
                sessionId, 
                process.StandardOutput, 
                cts.Token);

            _outputProcessors[sessionId] = StartOutputProcessorAsync(
                sessionId, 
                cts.Token);

            // Wait for the initial (gdb) prompt
            await WaitForInitialPromptAsync(sessionId, cts.Token);

            logger.LogInformation("Debug session {SessionId} ready", sessionId);
            return sessionId;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to launch debug session");
            await CleanupSessionAsync(sessionId);
            throw;
        }
    }

    /// <summary>
    /// Send a command and wait for response.
    /// </summary>
    public async Task<MiResponse?> SendCommandAsync(
        string sessionId,
        string command,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        timeout ??= TimeSpan.FromSeconds(DefaultTimeoutSeconds);

        if (!_inputStreams.TryGetValue(sessionId, out var value))
        {
            throw new InvalidOperationException($"Session {sessionId} not found");
        }

        TaskCompletionSource<MiResponse> tcs;
        int token;

        // PHASE 1: Register + Send (must be atomic/serialized)
        await _commandLock.WaitAsync(cancellationToken);
        try
        {
            // 1. Get token
            token = GetNextToken();

            // 2. Create and register TaskCompletionSource FIRST
            tcs = new TaskCompletionSource<MiResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pendingCmd = new PendingCommand
            {
                Token = token,
                Command = command,
                CompletionSource = tcs,
                SentAt = DateTime.UtcNow,
                ExpectsRunningState = IsExecutionCommand(command),
                State = CommandState.Sent
            };

            _pendingCommands[sessionId][token] = pendingCmd;

            // 3. NOW send command (TCS is already registered)
            var fullCommand = $"{token}{command}";
            await value.WriteLineAsync(fullCommand);
            await value.FlushAsync(cancellationToken);

            logger.LogDebug("Sent command {Token}: {Command}", token, command);
        }
        finally
        {
            _commandLock.Release();
        }

        // PHASE 2: Wait for response (outside lock - other commands can send)
        using var cts = new CancellationTokenSource(timeout.Value);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);

        linkedCts.Token.Register(() =>
        {
            if (cts.IsCancellationRequested)
            {
                logger.LogWarning("Command {Token} timed out after {Timeout}", token, timeout);
            }
            tcs.TrySetCanceled();
        });

        try
        {
            var response = await tcs.Task;
            logger.LogDebug("Command {Token} completed successfully", token);
            return response;
        }
        catch (OperationCanceledException)
        {
            logger.LogError("Command {Token} was cancelled or timed out", token);
            return null;
        }
        finally
        {
            // Clean up pending command entry
            if (_pendingCommands.TryGetValue(sessionId, out var commands))
            {
                commands.TryRemove(token, out _);
            }
        }
    }

    /// <summary>
    /// Disconnect a debug session.
    /// </summary>
    public async Task DisconnectAsync(string sessionId)
    {
        logger.LogInformation("Disconnecting session {SessionId}", sessionId);
        await CleanupSessionAsync(sessionId);
    }

    /// <summary>
    /// Get all active session IDs.
    /// </summary>
    public IReadOnlyList<string> GetActiveSessions()
    {
        return _debuggerProcesses.Keys.ToList();
    }

    private async Task StartBackgroundReaderAsync(
        string sessionId, 
        StreamReader output, 
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Starting background reader for session {SessionId}", sessionId);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await output.ReadLineAsync(cancellationToken);

                if (line == null)
                {
                    logger.LogWarning("stdout closed for session {SessionId}", sessionId);
                    await HandleStreamClosedAsync(sessionId);
                    break;
                }

                logger.LogTrace("[{SessionId}] MI RAW: {Line}", sessionId, line);

                // Queue for processing
                if (!_outputQueues.TryGetValue(sessionId, out var queue)) continue;
                if (!queue.TryAdd(line, TimeSpan.FromSeconds(1)))
                {
                    logger.LogError("Output queue full for session {SessionId}, dropping line", sessionId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Background reader stopped for session {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Background reader error for session {SessionId}", sessionId);
            await HandleReaderErrorAsync(sessionId, ex);
        }
    }

    private async Task StartOutputProcessorAsync(
        string sessionId, 
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Starting output processor for session {SessionId}", sessionId);

        if (!_outputQueues.TryGetValue(sessionId, out var queue))
        {
            logger.LogError("No output queue found for session {SessionId}", sessionId);
            return;
        }

        try
        {
            foreach (var line in queue.GetConsumingEnumerable(cancellationToken))
            {
                try
                {
                    await ProcessMiRecordAsync(sessionId, line);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error processing MI record: {Line}", line);
                }
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Output processor stopped for session {SessionId}", sessionId);
        }
    }

    private async Task ProcessMiRecordAsync(string sessionId, string line)
    {
        // Result record with token: 123^done,bkpt={...}
        if (Regex.IsMatch(line, @"^\d+\^"))
        {
            HandleResultRecord(sessionId, line);
            return;
        }

        // Async exec: *stopped,reason="breakpoint-hit"
        if (line.StartsWith("*"))
        {
            HandleAsyncExecRecord(sessionId, line);
            return;
        }

        // Async notify: =library-loaded,...
        if (line.StartsWith("="))
        {
            HandleAsyncNotifyRecord(sessionId, line);
            return;
        }

        // Stream output: ~"Hello\n"
        if (line.StartsWith("~") || line.StartsWith("@") || line.StartsWith("&"))
        {
            HandleStreamRecord(sessionId, line);
            return;
        }

        // Prompt: (gdb)
        if (line == "(gdb)")
        {
            HandlePrompt(sessionId);
            return;
        }

        logger.LogWarning("Unknown MI record format: {Line}", line);
        await Task.CompletedTask;
    }

    private void HandleResultRecord(string sessionId, string line)
    {
        var match = Regex.Match(line, @"^(\d+)\^(\w+)(?:,(.*))?$");
        if (!match.Success)
        {
            logger.LogWarning("Malformed result record: {Line}", line);
            return;
        }

        var token = int.Parse(match.Groups[1].Value);
        var resultClass = match.Groups[2].Value;
        var data = match.Groups[3].Success ? match.Groups[3].Value : null;

        if (!_pendingCommands[sessionId].TryGetValue(token, out var cmd))
        {
            logger.LogWarning("Received response for unknown token {Token}", token);
            return;
        }

        cmd.AccumulatedRecords.Add(line);

        // Update state based on result class
        cmd.State = resultClass switch
        {
            "done" => CommandState.DoneOk,
            "error" => CommandState.Error,
            "running" => CommandState.Running,
            _ => cmd.State
        };

        logger.LogTrace("Command {Token} state: {State}", token, cmd.State);
    }

    private void HandleAsyncExecRecord(string sessionId, string line)
    {
        var match = Regex.Match(line, @"^\*(\w+)(?:,(.*))?$");
        if (!match.Success)
        {
            logger.LogWarning("Malformed async exec record: {Line}", line);
            return;
        }

        var reason = match.Groups[1].Value;

        // If this is a *stopped event, attach it to any running command
        if (reason == "stopped")
        {
            var runningCmd = _pendingCommands[sessionId].Values
                .FirstOrDefault(c => c.State == CommandState.Running);

            if (runningCmd != null)
            {
                runningCmd.AccumulatedRecords.Add(line);
                runningCmd.State = CommandState.Stopped;
                logger.LogDebug("Command {Token} received *stopped", runningCmd.Token);
            }
        }

        // Always fire event for async exec records
        FireAsyncEvent(sessionId, line, reason);
    }

    private void HandleAsyncNotifyRecord(string sessionId, string line)
    {
        var match = Regex.Match(line, @"^=(\S+)(?:,(.*))?$");
        if (!match.Success)
        {
            logger.LogWarning("Malformed async notify record: {Line}", line);
            return;
        }

        var eventType = match.Groups[1].Value;

        // Fire event for async notify records
        FireAsyncEvent(sessionId, line, eventType);
    }

    private void HandleStreamRecord(string sessionId, string line)
    {
        // Parse stream record: ~"text\n"
        var match = Regex.Match(line, @"^[~@&]""(.*)""$");
        if (!match.Success) return;
        var content = match.Groups[1].Value;
            
        // Unescape content
        content = Regex.Unescape(content);

        FireConsoleOutput(sessionId, line[0], content);
    }

    private void HandlePrompt(string sessionId)
    {
        if (!_pendingCommands.TryGetValue(sessionId, out var commands))
            return;
        
        if (commands.IsEmpty)
        {
            FireAsyncEvent(sessionId, "(gdb)", "prompt-ready");
            return;
        }

        // Check which pending commands can complete
        foreach (var cmd in commands.Values.ToList())
        {
            var shouldComplete = cmd.State switch
            {
                CommandState.DoneOk => true,
                CommandState.Error => true,
                CommandState.Stopped => true,
                CommandState.Running => false, // Still waiting for *stopped
                CommandState.Sent => false,    // Still waiting for result
                _ => false
            };

            if (!shouldComplete) continue;
            cmd.State = CommandState.Complete;

            var response = new MiResponse
            {
                Token = cmd.Token,
                Success = cmd.State == CommandState.DoneOk || cmd.State == CommandState.Stopped,
                ResultClass = DetermineResultClass(cmd),
                Records = cmd.AccumulatedRecords.ToList()
            };

            cmd.CompletionSource.TrySetResult(response);
            logger.LogDebug("Command {Token} completed with state {State}", cmd.Token, cmd.State);
        }
    }

    private static string DetermineResultClass(PendingCommand cmd)
    {
        var resultRecord = cmd.AccumulatedRecords
            .FirstOrDefault(r => Regex.IsMatch(r, @"^\d+\^"));

        if (resultRecord != null)
        {
            var match = Regex.Match(resultRecord, @"^\d+\^(\w+)");
            if (match.Success)
                return match.Groups[1].Value;
        }

        return "done";
    }

    private void FireAsyncEvent(string sessionId, string record, string eventType)
    {
        // Don't block the reader thread
        Task.Run(() =>
        {
            try
            {
                var args = new MiAsyncEventArgs
                {
                    SessionId = sessionId,
                    EventType = eventType,
                    RawRecord = record,
                    ParsedData = ParseMiRecord(record)
                };

                AsyncEventReceived?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error firing async event");
            }
        });
    }

    private void FireConsoleOutput(string sessionId, char streamType, string content)
    {
        Task.Run(() =>
        {
            try
            {
                var args = new ConsoleOutputEventArgs
                {
                    SessionId = sessionId,
                    StreamType = streamType,
                    Content = content
                };

                ConsoleOutputReceived?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error firing console output event");
            }
        });
    }

    private Dictionary<string, string> ParseMiRecord(string record)
    {
        var result = new Dictionary<string, string>();

        // Simple key=value parsing (would need a more sophisticated parser for production)
        var matches = Regex.Matches(record, @"(\w+)=""([^""]*)""|(\w+)=(\{[^}]*\})|(\w+)=(\w+)");
        
        foreach (Match match in matches)
        {
            if (match.Groups[1].Success)
            {
                result[match.Groups[1].Value] = match.Groups[2].Value;
            }
            else if (match.Groups[3].Success)
            {
                result[match.Groups[3].Value] = match.Groups[4].Value;
            }
            else if (match.Groups[5].Success)
            {
                result[match.Groups[5].Value] = match.Groups[6].Value;
            }
        }

        return result;
    }

    private async Task WaitForInitialPromptAsync(string sessionId, CancellationToken cancellationToken)
    {
        // Wait up to 5 seconds for initial (gdb) prompt
        var timeout = TimeSpan.FromSeconds(5);
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        var tcs = new TaskCompletionSource<bool>();

        AsyncEventReceived += OnAsyncEvent;

        try
        {
            await tcs.Task.WaitAsync(cts.Token);
        }
        finally
        {
            AsyncEventReceived -= OnAsyncEvent;
        }

        return;

        void OnAsyncEvent(object? sender, MiAsyncEventArgs e)
        {
            if (e.SessionId == sessionId)
            {
                tcs.TrySetResult(true);
            }
        }
    }

    private async Task HandleStreamClosedAsync(string sessionId)
    {
        logger.LogWarning("Stream closed for session {SessionId}, failing pending commands", sessionId);

        // Fail all pending commands
        if (_pendingCommands.TryGetValue(sessionId, out var commands))
        {
            foreach (var cmd in commands.Values)
            {
                cmd.CompletionSource.TrySetException(
                    new InvalidOperationException("Debugger process exited unexpectedly")
                );
            }
            commands.Clear();
        }

        // Fire session disconnected event
        await Task.Run(() =>
        {
            try
            {
                SessionDisconnected?.Invoke(this, new SessionDisconnectedEventArgs
                {
                    SessionId = sessionId,
                    Reason = "Stream closed"
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error firing session disconnected event");
            }
        });

        await CleanupSessionAsync(sessionId);
    }

    private async Task HandleReaderErrorAsync(string sessionId, Exception exception)
    {
        logger.LogError(exception, "Reader error for session {SessionId}", sessionId);

        // Fail all pending commands
        if (_pendingCommands.TryGetValue(sessionId, out var commands))
        {
            foreach (var cmd in commands.Values)
            {
                cmd.CompletionSource.TrySetException(exception);
            }
            commands.Clear();
        }

        // Fire session disconnected event
        await Task.Run(() =>
        {
            try
            {
                SessionDisconnected?.Invoke(this, new SessionDisconnectedEventArgs
                {
                    SessionId = sessionId,
                    Reason = $"Error: {exception.Message}"
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error firing session disconnected event");
            }
        });

        await CleanupSessionAsync(sessionId);
    }

    private async Task CleanupSessionAsync(string sessionId)
    {
        logger.LogInformation("Cleaning up session {SessionId}", sessionId);

        // 1. Cancel background reader
        if (_readerCancellations.TryGetValue(sessionId, out var cts))
        {
            await cts.CancelAsync();
            _readerCancellations.Remove(sessionId);
        }

        // 2. Wait for reader and processor to stop (with timeout)
        var tasks = new List<Task>();
        if (_outputReaders.TryGetValue(sessionId, out var readerTask))
        {
            tasks.Add(readerTask);
            _outputReaders.Remove(sessionId);
        }
        if (_outputProcessors.TryGetValue(sessionId, out var processorTask))
        {
            tasks.Add(processorTask);
            _outputProcessors.Remove(sessionId);
        }

        if (tasks.Count > 0)
        {
            await Task.WhenAny(Task.WhenAll(tasks), Task.Delay(5000));
        }

        // 3. Dispose queue
        if (_outputQueues.TryGetValue(sessionId, out var queue))
        {
            queue.CompleteAdding();
            queue.Dispose();
            _outputQueues.Remove(sessionId);
        }

        // 4. Clean up pending commands
        _pendingCommands.Remove(sessionId, out _);

        // 5. Close streams
        if (_inputStreams.TryGetValue(sessionId, out var inputStream))
        {
            await inputStream.DisposeAsync();
            _inputStreams.Remove(sessionId);
        }

        // 6. Kill the process if still running
        if (_debuggerProcesses.TryGetValue(sessionId, out var process))
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await Task.Run(() => process.WaitForExit(5000));
                }
                process.Dispose();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error killing process for session {SessionId}", sessionId);
            }
            _debuggerProcesses.Remove(sessionId);
        }

        logger.LogInformation("Session {SessionId} cleanup complete", sessionId);
    }

    private int GetNextToken()
    {
        var token = Interlocked.Increment(ref _nextToken);
        
        // Reset on overflow
        if (_nextToken > 999999)
        {
            Interlocked.Exchange(ref _nextToken, 1);
        }

        return token;
    }

    private static bool IsExecutionCommand(string command)
    {
        return command.StartsWith("-exec-run") ||
               command.StartsWith("-exec-continue") ||
               command.StartsWith("-exec-step") ||
               command.StartsWith("-exec-next") ||
               command.StartsWith("-exec-finish");
    }

    private static string FindNetcoredbg()
    {
        // Try PATH environment variable
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (pathEnv != null)
        {
            foreach (var path in pathEnv.Split(Path.PathSeparator))
            {
                var fullPath = Path.Combine(path, "netcoredbg.exe");
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
        }

        // Try common installation locations
        string[] commonPaths =
        [
            @"C:\Program Files\netcoredbg\netcoredbg.exe",
            @"C:\Program Files (x86)\netcoredbg\netcoredbg.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet", "tools", "netcoredbg.exe")
        ];

        foreach (var path in commonPaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return string.Empty;
    }

    private static string BuildCommandLine(string executablePath, string[]? arguments)
    {
        var commandLine = $"--interpreter=mi -- dotnet \"{executablePath}\"";

        if (arguments != null && arguments.Length > 0)
        {
            commandLine += " " + string.Join(" ", arguments.Select(arg => $"\"{arg}\""));
        }

        return commandLine;
    }

    public void Dispose()
    {
        foreach (var sessionId in _debuggerProcesses.Keys.ToList())
        {
            CleanupSessionAsync(sessionId).Wait(TimeSpan.FromSeconds(5));
        }

        _commandLock.Dispose();
    }
}