# MI Protocol Synchronization & Race Condition Handling

## The Concurrent Execution Problem

### Scenario: Race Condition During Command Sending

```
Timeline:
Thread A (Sending):              Thread B (Background Reader):
─────────────────────            ──────────────────────────────
1. Format command string
2. Get next token (123)
3. Write to stdin: "123-exec-continue\n"
4. Flush stdin
                                 5. Read stdout: "123^running"
                                 6. Parse token: 123
                                 7. Look up pending command... NOT FOUND!
5. Create TaskCompletionSource
6. Register in _pendingCommands[123]
7. Wait for response... (never arrives!)
```

**Result:** Response arrives and is processed BEFORE the awaiting TaskCompletionSource is registered. The response is lost, the command hangs until timeout.

---

## Solution: Register-Before-Send Pattern

### Core Principle
**Always register the expectation BEFORE sending the command.**

This ensures the background reader can never receive a response before we're ready to handle it.

### Implementation Pattern

```csharp
public async Task<MiResponse?> SendCommandAsync(
    string sessionId, 
    string command,
    TimeSpan? timeout = null)
{
    timeout ??= TimeSpan.FromSeconds(30);
    
    // PHASE 1: Register + Send (must be atomic/serialized)
    TaskCompletionSource<MiResponse> tcs;
    int token;
    
    await _commandLock.WaitAsync(); // Serialize this critical section
    try
    {
        // 1. Get token
        token = GetNextToken();
        
        // 2. Create and register TaskCompletionSource FIRST
        tcs = new TaskCompletionSource<MiResponse>();
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
        await _inputStreams[sessionId].WriteLineAsync($"{token}{command}");
        await _inputStreams[sessionId].FlushAsync();
        
        logger.LogDebug("Sent command {Token}: {Command}", token, command);
    }
    finally
    {
        _commandLock.Release(); // Release immediately after send
    }
    
    // PHASE 2: Wait for response (outside lock - other commands can send)
    using var cts = new CancellationTokenSource(timeout.Value);
    cts.Token.Register(() => 
    {
        tcs.TrySetCanceled();
        logger.LogWarning("Command {Token} timed out after {Timeout}", token, timeout);
    });
    
    try
    {
        MiResponse response = await tcs.Task;
        logger.LogDebug("Command {Token} completed successfully", token);
        return response;
    }
    catch (OperationCanceledException)
    {
        logger.LogError("Command {Token} was cancelled", token);
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

private static bool IsExecutionCommand(string command)
{
    return command.StartsWith("-exec-run") ||
           command.StartsWith("-exec-continue") ||
           command.StartsWith("-exec-step") ||
           command.StartsWith("-exec-next") ||
           command.StartsWith("-exec-finish");
}
```

---

## Synchronization Points

### 1. Command Sending Lock (Critical Section)

**Why needed:**
- Prevents command text from interleaving in stdin
- Ensures token registration is atomic with sending
- Prevents race between Thread A and Thread B

**What's protected:**
```csharp
await _commandLock.WaitAsync();
try
{
    // ATOMIC SECTION:
    // 1. Get token
    // 2. Register TaskCompletionSource
    // 3. Write to stdin
    // 4. Flush stdin
}
finally
{
    _commandLock.Release();
}
```

**What's NOT protected:**
- Waiting for response (happens outside lock)
- Multiple commands can be "in flight" simultaneously
- Background reader operates independently

### 2. Pending Commands Dictionary (Thread-Safe)

```csharp
// Use ConcurrentDictionary for each session
private readonly Dictionary<string, ConcurrentDictionary<int, PendingCommand>> _pendingCommands = new();

// Thread-safe operations:
_pendingCommands[sessionId][token] = cmd;           // Add (from sender)
_pendingCommands[sessionId].TryGetValue(token, out cmd);  // Read (from reader)
_pendingCommands[sessionId].TryRemove(token, out cmd);    // Remove (from sender cleanup)
```

### 3. Output Queue (Thread-Safe)

```csharp
// Use BlockingCollection for thread-safe queuing
private readonly Dictionary<string, BlockingCollection<string>> _outputQueues = new();

// Producer (background reader):
_outputQueues[sessionId].Add(line);

// Consumer (processing task):
foreach (string line in _outputQueues[sessionId].GetConsumingEnumerable(cancellationToken))
{
    await ProcessMiRecordAsync(sessionId, line);
}
```

---

## Command State Machine

Each pending command goes through states:

```
┌─────────┐
│  SENT   │ ← Command sent, waiting for any response
└────┬────┘
     │
     ├─────────────────┬────────────────┐
     │                 │                │
     ▼                 ▼                ▼
┌─────────┐      ┌──────────┐    ┌─────────┐
│ RUNNING │      │ DONE_OK  │    │  ERROR  │
└────┬────┘      └────┬─────┘    └────┬────┘
     │                │                │
     │ *stopped       │ (gdb)          │ (gdb)
     ▼                ▼                ▼
┌─────────┐      ┌──────────┐    ┌─────────┐
│STOPPED  │      │COMPLETE  │    │COMPLETE │
└────┬────┘      └──────────┘    └─────────┘
     │
     │ (gdb)
     ▼
┌─────────┐
│COMPLETE │
└─────────┘
```

### State Transitions

```csharp
public enum CommandState
{
    Sent,           // Command sent, awaiting response
    Running,        // Got ^running, waiting for *stopped
    Stopped,        // Got *stopped, waiting for (gdb)
    DoneOk,         // Got ^done, waiting for (gdb)
    Error,          // Got ^error, waiting for (gdb)
    Complete        // Got (gdb), ready to return
}

private class PendingCommand
{
    public int Token { get; set; }
    public string Command { get; set; } = string.Empty;
    public TaskCompletionSource<MiResponse> CompletionSource { get; set; } = null!;
    public List<string> AccumulatedRecords { get; set; } = new();
    public DateTime SentAt { get; set; }
    public bool ExpectsRunningState { get; set; }
    public CommandState State { get; set; } = CommandState.Sent;
}
```

---

## Background Reader Processing Flow

### Main Processing Loop

```csharp
private async Task ProcessOutputQueueAsync(string sessionId, CancellationToken cancellationToken)
{
    var queue = _outputQueues[sessionId];
    
    try
    {
        foreach (string line in queue.GetConsumingEnumerable(cancellationToken))
        {
            logger.LogTrace("[{SessionId}] MI: {Line}", sessionId, line);
            
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
        logger.LogInformation("Output queue processor stopped for session {SessionId}", sessionId);
    }
}
```

### Record Type Dispatch

```csharp
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
}
```

---

## Handling Different Response Types

### 1. Simple Success Response (^done)

```
Send: 123-break-insert Program.cs:10
Recv: 123^done,bkpt={number="1",...}
Recv: (gdb)
```

**Handler:**
```csharp
private void HandleResultRecord(string sessionId, string line)
{
    var match = Regex.Match(line, @"^(\d+)\^(\w+)(?:,(.*))?$");
    if (!match.Success) return;
    
    int token = int.Parse(match.Groups[1].Value);
    string resultClass = match.Groups[2].Value;
    string? data = match.Groups[3].Success ? match.Groups[3].Value : null;
    
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
}
```

### 2. Running Command (^running → *stopped)

```
Send: 123-exec-continue
Recv: 123^running
Recv: (gdb)           ← First prompt, command accepted
      [execution...]
Recv: *stopped,reason="breakpoint-hit",...
Recv: (gdb)           ← Second prompt, ready for next command
```

**Handler:**
```csharp
private void HandleAsyncExecRecord(string sessionId, string line)
{
    // Parse async exec record
    var match = Regex.Match(line, @"^\*(\w+)(?:,(.*))?$");
    if (!match.Success) return;
    
    string reason = match.Groups[1].Value;
    
    if (reason == "stopped")
    {
        // Find any command in Running state
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
```

### 3. Prompt Handling ((gdb))

The prompt signals "ready for next command" - check which pending commands can complete:

```csharp
private void HandlePrompt(string sessionId)
{
    if (!_pendingCommands.TryGetValue(sessionId, out var commands))
        return;
    
    foreach (var cmd in commands.Values.ToList())
    {
        bool shouldComplete = cmd.State switch
        {
            CommandState.DoneOk => true,   // ^done + (gdb) = complete
            CommandState.Error => true,    // ^error + (gdb) = complete
            CommandState.Stopped => true,  // *stopped + (gdb) = complete
            CommandState.Running => false, // Still waiting for *stopped
            CommandState.Sent => false,    // Still waiting for result
            _ => false
        };
        
        if (shouldComplete)
        {
            cmd.State = CommandState.Complete;
            
            var response = new MiResponse
            {
                Token = cmd.Token,
                Success = cmd.State == CommandState.DoneOk,
                ResultClass = DetermineResultClass(cmd),
                Records = cmd.AccumulatedRecords.ToList()
            };
            
            cmd.CompletionSource.TrySetResult(response);
            logger.LogDebug("Command {Token} completed", cmd.Token);
        }
    }
}

private static string DetermineResultClass(PendingCommand cmd)
{
    // Find the result record in accumulated records
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
```

---

## Async Event Distribution (Non-Blocking)

### Fire-and-Forget Pattern

Events should NOT block the background reader:

```csharp
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
```

### Event Subscription

```csharp
// In DebugSessionManager or MCP tool:
miClient.AsyncEventReceived += OnDebuggerEvent;

private void OnDebuggerEvent(object? sender, MiAsyncEventArgs e)
{
    logger.LogInformation("[{SessionId}] Event: {Type}", e.SessionId, e.EventType);
    
    switch (e.EventType)
    {
        case "stopped":
            HandleProgramStopped(e);
            break;
        case "library-loaded":
            HandleLibraryLoaded(e);
            break;
        case "thread-created":
            HandleThreadCreated(e);
            break;
    }
}
```

---

## Timeout Handling

### Timeout with Cleanup

```csharp
public async Task<MiResponse?> SendCommandAsync(...)
{
    // ... registration and sending ...
    
    using var cts = new CancellationTokenSource(timeout.Value);
    
    // Register cleanup action on timeout
    cts.Token.Register(() =>
    {
        // Mark command as timed out
        if (_pendingCommands[sessionId].TryGetValue(token, out var cmd))
        {
            cmd.CompletionSource.TrySetCanceled();
            logger.LogWarning(
                "Command {Token} ({Command}) timed out after {Timeout}",
                token, command, timeout);
        }
    });
    
    try
    {
        return await tcs.Task;
    }
    catch (OperationCanceledException)
    {
        // Command timed out
        return null;
    }
    finally
    {
        // Always clean up
        _pendingCommands[sessionId].TryRemove(token, out _);
    }
}
```

---

## Handling Concurrent Commands

### Multiple Commands In-Flight

The design allows multiple commands to be waiting for responses simultaneously:

```
Thread 1: Send command 123 → Wait for response
Thread 2: Send command 124 → Wait for response
Thread 3: Send command 125 → Wait for response

Background Reader:
  Receives: 123^done → Completes command 123
  Receives: (gdb)
  Receives: 125^done → Completes command 125
  Receives: (gdb)
  Receives: 124^done → Completes command 124
  Receives: (gdb)
```

Each command waits independently on its own TaskCompletionSource. Order of completion doesn't matter.

### Serialized Sending, Parallel Waiting

```
Time →
─────────────────────────────────────────
Thread A: |SEND123|──wait───────────────> 123^done
Thread B:    |SEND124|──wait────────────> 124^done
Thread C:       |SEND125|──wait─────────> 125^done

_commandLock serializes SEND operations only.
Waiting happens in parallel without lock.
```

---

## Edge Cases & Error Scenarios

### 1. Response Arrives During Token Assignment

**Can't happen** - token is assigned and registered before sending.

### 2. Process Crashes During Command

```csharp
// In background reader:
private async Task BackgroundReaderAsync(string sessionId, ...)
{
    try
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            string? line = await output.ReadLineAsync(cancellationToken);
            
            if (line == null)
            {
                // Stream closed - process exited
                logger.LogWarning("Process stdout closed for session {SessionId}", sessionId);
                await HandleProcessExitAsync(sessionId);
                break;
            }
            
            _outputQueues[sessionId].Add(line);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Background reader error for session {SessionId}", sessionId);
        await HandleReaderErrorAsync(sessionId, ex);
    }
}

private async Task HandleProcessExitAsync(string sessionId)
{
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
    SessionDisconnected?.Invoke(this, new SessionDisconnectedEventArgs
    {
        SessionId = sessionId,
        Reason = "Process exited"
    });
}
```

### 3. Malformed Response

```csharp
private void HandleResultRecord(string sessionId, string line)
{
    var match = Regex.Match(line, @"^(\d+)\^(\w+)(?:,(.*))?$");
    
    if (!match.Success)
    {
        logger.LogWarning("Malformed result record: {Line}", line);
        // Continue processing - don't crash the reader
        return;
    }
    
    // ... process normally ...
}
```

### 4. Duplicate Token (Should Never Happen)

```csharp
if (_pendingCommands[sessionId].ContainsKey(token))
{
    logger.LogError("Token collision detected: {Token}. This should never happen!", token);
    // Use next available token
    token = GetNextAvailableToken(sessionId);
}
```

---

## Performance Considerations

### 1. Lock Contention

**Minimize lock hold time:**
```csharp
// BAD: Hold lock during write AND flush AND wait
await _commandLock.WaitAsync();
try
{
    await WriteAsync(...);
    await FlushAsync(...);
    return await WaitForResponseAsync(...); // BLOCKS OTHER COMMANDS!
}
finally { _commandLock.Release(); }

// GOOD: Hold lock only during write/flush
await _commandLock.WaitAsync();
try
{
    await WriteAsync(...);
    await FlushAsync(...);
}
finally { _commandLock.Release(); }
return await WaitForResponseAsync(...); // Outside lock
```

### 2. Queue Capacity

```csharp
// Bounded queue to prevent memory exhaustion
private const int MaxQueueSize = 10000;

_outputQueues[sessionId] = new BlockingCollection<string>(MaxQueueSize);

// If queue fills (shouldn't happen in practice):
if (!queue.TryAdd(line, TimeSpan.FromSeconds(1)))
{
    logger.LogError("Output queue full for session {SessionId} - dropping line", sessionId);
    // Or: throw exception to kill session
}
```

### 3. Event Handler Performance

```csharp
// Fire events asynchronously to avoid blocking
Task.Run(() => FireEvent(...));

// OR use event queue with dedicated processing thread
_eventQueue.Add(eventArgs);
```

---

## Testing Strategies

### 1. Race Condition Tests

```csharp
[Fact]
public async Task MultipleRapidCommands_NoLostResponses()
{
    // Send 100 commands as fast as possible
    var tasks = Enumerable.Range(0, 100)
        .Select(i => client.SetBreakpointAsync(sessionId, $"File{i}.cs", i))
        .ToList();
    
    var results = await Task.WhenAll(tasks);
    
    // All should complete successfully
    Assert.All(results, r => Assert.NotNull(r));
}
```

### 2. Async Event Tests

```csharp
[Fact]
public async Task AsyncEvents_ReceivedDuringCommandExecution()
{
    var events = new List<MiAsyncEventArgs>();
    client.AsyncEventReceived += (s, e) => events.Add(e);
    
    // Start program (will load libraries)
    await client.RunAsync(sessionId);
    
    // Should have received library-loaded events
    Assert.Contains(events, e => e.EventType == "library-loaded");
}
```

### 3. Timeout Tests

```csharp
[Fact]
public async Task CommandTimeout_CleansUpPendingCommand()
{
    // Send command that will never respond
    var result = await client.SendCommandAsync(
        sessionId, 
        "-invalid-command",
        TimeSpan.FromSeconds(1));
    
    Assert.Null(result); // Timed out
    
    // Pending commands should be cleaned up
    Assert.Empty(client.GetPendingCommands(sessionId));
}
```

---

## Summary: Key Design Decisions

1. ✅ **Register TaskCompletionSource BEFORE sending command** - Prevents race condition
2. ✅ **Serialize command sending with lock** - Prevents command interleaving
3. ✅ **Release lock before waiting** - Allows concurrent commands
4. ✅ **Use ConcurrentDictionary for pending commands** - Thread-safe lookup
5. ✅ **Use BlockingCollection for output queue** - Thread-safe producer/consumer
6. ✅ **State machine per command** - Handles complex response sequences
7. ✅ **Fire events asynchronously** - Don't block background reader
8. ✅ **Clean up on timeout** - Prevent memory leaks
9. ✅ **Handle process exit gracefully** - Fail pending commands
10. ✅ **Log all state transitions** - Essential for debugging

---

**Last Updated:** 2025-10-27
**Status:** Design Complete - Ready for Implementation
**Related:** MI_PROTOCOL_FLOW.md, MI_ASYNC_DESIGN_REQUIREMENTS.md