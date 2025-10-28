# MI Protocol Async Design Requirements

## Problem Statement

The current `MiClient.cs` implementation has a **critical architectural flaw**: it only reads stdout synchronously when expecting a response to a command. This causes multiple issues:

### Current Architecture Issues

1. **Lost Async Events**
    - MI async notifications (`*stopped`, `=library-loaded`, `=thread-created`, etc.) can arrive at ANY time
    - Current code only reads during `ReadResponseAsync`, missing events between commands
    - Events arriving while processing another command get lost or cause deadlocks

2. **Blocking Reads**
    - `ReadLineAsync` in `ReadResponseAsync` blocks the thread
    - If an async event arrives before the expected response, it will be consumed by the wrong command handler
    - No way to handle out-of-order responses

3. **No Event Distribution**
    - Async events like `=library-loaded` need to be visible to the application
    - No mechanism to notify MCP tools about debugger events
    - Can't react to program state changes (breakpoint hit, thread created, etc.)

4. **Race Conditions**
    - Multiple commands sent in quick succession could read each other's responses
    - Token matching happens AFTER reading, not during buffering
    - `^running` followed by `*stopped` can arrive before next command is sent

### Example Failure Scenario

```
Timeline:
1. Send: -exec-run
2. Receive: ^running
3. Receive: =library-loaded (LOST - not being read)
4. Receive: =library-loaded (LOST)
5. Receive: =thread-created (LOST)
6. Receive: *stopped,reason="entry-point-hit"
7. Code tries to read response, but stdout buffer is full of unread lines
8. Deadlock or timeout
```

---

## Design Requirements

### Requirement 1: Background Reading Thread

**Must Have:**
- Dedicated background task per debug session
- Continuously reads stdout line-by-line
- Runs independently of command sending
- Thread-safe queuing of all MI records
- Graceful shutdown on session close

**Architecture:**
```
Process.StandardOutput
    ↓ (background task continuously reads)
BlockingCollection<string> (thread-safe queue)
    ↓ (parsing & distribution)
    ├→ Command Response Matching (by token)
    ├→ Async Event Handler (notifications)
    └→ Stream Output Handler (console output)
```

### Requirement 2: Record Type Classification

**All MI records must be classified:**

| Record Type | Prefix | Examples | Handling |
|-------------|--------|----------|----------|
| Result | `^` | `^done`, `^running`, `^error` | Match to command by token |
| Async Exec | `*` | `*stopped`, `*running` | Fire event + match to command |
| Async Notify | `=` | `=library-loaded`, `=thread-created` | Fire event only |
| Stream Console | `~` | `~"Hello World\n"` | Log or return to caller |
| Stream Target | `@` | Remote protocol output | Log |
| Stream Log | `&` | Debug log messages | Log |
| Prompt | `(gdb)` | End of response | Signal completion |

### Requirement 3: Command-Response Matching

**Token-Based Matching:**
```csharp
// Command sent: 123-break-insert Program.cs:10
// Response:     123^done,bkpt={...}
//               (gdb)

// Matching logic:
1. Parse token from response: 123
2. Look up pending command with token 123
3. Complete that command's TaskCompletionSource
4. Continue reading for next record
```

**Response Accumulation:**
- Some commands get multiple responses (e.g., `^running` then `*stopped`)
- Must accumulate all related records before completing command
- Use `(gdb)` prompt as signal that response is complete

### Requirement 4: Async Event Distribution

**Event Types to Surface:**

1. **Execution State Changes**
    - `*stopped` - Program stopped (breakpoint, step, crash, exit)
    - `*running` - Program resumed execution

2. **Module/Library Events**
    - `=library-loaded` - Assembly/DLL loaded
    - `=library-unloaded` - Assembly/DLL unloaded

3. **Thread Events**
    - `=thread-created` - New thread started
    - `=thread-exited` - Thread terminated
    - `=thread-selected` - Active thread changed

4. **Breakpoint Events**
    - `=breakpoint-modified` - Breakpoint resolved/changed
    - `=breakpoint-deleted` - Breakpoint removed

**Event Handler Interface:**
```csharp
public event EventHandler<MiAsyncEventArgs>? AsyncEventReceived;

public class MiAsyncEventArgs : EventArgs
{
    public string SessionId { get; set; }
    public string EventType { get; set; }  // "stopped", "library-loaded", etc.
    public string RawRecord { get; set; }
    public Dictionary<string, string> ParsedData { get; set; }
}
```

### Requirement 5: Thread Safety

**Concurrent Access Patterns:**
- Multiple threads sending commands simultaneously
- Background reader continuously writing to queue
- Event handlers reading from queue
- Session cleanup must be thread-safe

**Synchronization Requirements:**
```csharp
// Thread-safe collections
BlockingCollection<string> _outputQueue;
ConcurrentDictionary<int, TaskCompletionSource<MiResponse>> _pendingCommands;
SemaphoreSlim _commandSemaphore; // Serialize command sending

// Safe shutdown
CancellationTokenSource _readerCancellation;
```

### Requirement 6: Timeout Handling

**Commands must timeout:**
- Default: 30 seconds
- Configurable per command
- Must clean up pending command state on timeout
- Should not block other commands

```csharp
public async Task<MiResponse?> SendCommandAsync(
    string sessionId, 
    string command,
    TimeSpan? timeout = null)
{
    timeout ??= TimeSpan.FromSeconds(30);
    
    // Use TaskCompletionSource with timeout
    using var cts = new CancellationTokenSource(timeout.Value);
    // ...
}
```

### Requirement 7: Error Recovery

**Handle:**
- Process crash/exit
- Stdout stream closed unexpectedly
- Malformed MI records
- Timeout without response
- Partial reads

**Recovery Strategy:**
```csharp
try
{
    string? line = await reader.ReadLineAsync(cancellationToken);
    if (line == null)
    {
        // Stream closed - mark all pending commands as failed
        // Fire session disconnected event
        // Clean up resources
    }
}
catch (Exception ex)
{
    // Log error
    // Mark session as faulted
    // Fail pending commands
}
```

---

## Proposed Architecture

### Class Structure

```
MiClient
├─ Private Fields
│  ├─ _debuggerProcesses: Dictionary<string, Process>
│  ├─ _inputStreams: Dictionary<string, StreamWriter>
│  ├─ _outputReaders: Dictionary<string, Task> (background readers)
│  ├─ _outputQueues: Dictionary<string, BlockingCollection<string>>
│  ├─ _pendingCommands: Dictionary<string, ConcurrentDictionary<int, PendingCommand>>
│  ├─ _readerCancellations: Dictionary<string, CancellationTokenSource>
│  └─ _nextToken: int
│
├─ Public Events
│  ├─ AsyncEventReceived: EventHandler<MiAsyncEventArgs>
│  ├─ ConsoleOutputReceived: EventHandler<ConsoleOutputEventArgs>
│  └─ SessionDisconnected: EventHandler<SessionDisconnectedEventArgs>
│
├─ Public Methods
│  ├─ LaunchAsync(DebugSession)
│  ├─ DisconnectAsync(string sessionId)
│  ├─ SendCommandAsync(sessionId, command, timeout)
│  ├─ SetBreakpointAsync(...)
│  ├─ RunAsync(...)
│  └─ [... other MI commands ...]
│
└─ Private Methods
   ├─ StartBackgroundReaderAsync(sessionId)
   ├─ ProcessMiRecordAsync(sessionId, line)
   ├─ MatchResponseToCommand(sessionId, token, record)
   ├─ FireAsyncEvent(sessionId, record)
   └─ CleanupSession(sessionId)
```

### PendingCommand Class

```csharp
private class PendingCommand
{
    public int Token { get; set; }
    public string Command { get; set; }
    public TaskCompletionSource<MiResponse> CompletionSource { get; set; }
    public List<string> AccumulatedRecords { get; set; } = new();
    public DateTime SentAt { get; set; }
    public bool ExpectsRunningState { get; set; } // For exec-run, exec-continue
}
```

### Background Reader Pattern

```csharp
private async Task StartBackgroundReaderAsync(string sessionId, StreamReader output, CancellationToken cancellationToken)
{
    try
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            string? line = await output.ReadLineAsync(cancellationToken);
            
            if (line == null)
            {
                logger.LogWarning("stdout closed for session {SessionId}", sessionId);
                await HandleStreamClosedAsync(sessionId);
                break;
            }
            
            logger.LogTrace("MI: {Line}", line);
            
            // Queue for processing
            if (_outputQueues.TryGetValue(sessionId, out var queue))
            {
                queue.Add(line);
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
```

### Record Processing Pattern

```csharp
private async Task ProcessMiRecordAsync(string sessionId, string line)
{
    // Result record with token: 123^done,bkpt={...}
    if (Regex.IsMatch(line, @"^\d+\^"))
    {
        await HandleResultRecordAsync(sessionId, line);
        return;
    }
    
    // Async exec: *stopped,reason="breakpoint-hit"
    if (line.StartsWith("*"))
    {
        await HandleAsyncExecRecordAsync(sessionId, line);
        return;
    }
    
    // Async notify: =library-loaded,...
    if (line.StartsWith("="))
    {
        await HandleAsyncNotifyRecordAsync(sessionId, line);
        return;
    }
    
    // Stream output: ~"Hello\n"
    if (line.StartsWith("~") || line.StartsWith("@") || line.StartsWith("&"))
    {
        await HandleStreamRecordAsync(sessionId, line);
        return;
    }
    
    // Prompt: (gdb)
    if (line == "(gdb)")
    {
        await HandlePromptAsync(sessionId);
        return;
    }
    
    // Unknown format
    logger.LogWarning("Unknown MI record format: {Line}", line);
}
```

---

## Implementation Phases

### Phase 1: Background Reader Infrastructure (Critical)

**Estimated: 4-6 hours**

Tasks:
1. Add background reader fields to `MiClient`
2. Implement `StartBackgroundReaderAsync` method
3. Create thread-safe output queue per session
4. Add cancellation token management
5. Update `LaunchAsync` to start background reader
6. Update `DisconnectAsync` to stop background reader cleanly

**Success Criteria:**
- Background task starts on launch
- All stdout lines are queued
- No blocking reads in main thread
- Clean shutdown without exceptions

### Phase 2: Command-Response Matching (Critical)

**Estimated: 4-6 hours**

Tasks:
1. Create `PendingCommand` class
2. Add token-to-command mapping dictionary
3. Implement `MatchResponseToCommand` logic
4. Refactor `SendCommandAsync` to use TaskCompletionSource
5. Handle `^running` + `*stopped` sequence properly
6. Implement timeout with CancellationToken

**Success Criteria:**
- Commands receive correct responses
- Multiple simultaneous commands don't interfere
- Timeouts work correctly
- `^running` commands wait for `*stopped`

### Phase 3: Async Event Distribution (High Priority)

**Estimated: 3-4 hours**

Tasks:
1. Define event argument classes
2. Add public event handlers to `MiClient`
3. Implement `FireAsyncEvent` method
4. Parse common async records (stopped, library-loaded, etc.)
5. Add event subscription in debug session manager

**Success Criteria:**
- Events fire for all async notifications
- Parsed data is accessible to subscribers
- Events don't block the reader thread
- Thread-safe event invocation

### Phase 4: Error Handling & Recovery (Medium Priority)

**Estimated: 2-3 hours**

Tasks:
1. Add stream closed detection
2. Implement `HandleReaderErrorAsync`
3. Fail pending commands on error
4. Fire session disconnected event
5. Add retry logic for transient errors
6. Log comprehensive diagnostics

**Success Criteria:**
- Graceful handling of process crashes
- Pending commands fail with clear errors
- Session state is cleaned up
- No resource leaks

### Phase 5: Testing & Validation (High Priority)

**Estimated: 4-5 hours**

Tasks:
1. Test with multiple breakpoints
2. Test rapid command sequences
3. Test crash scenarios
4. Test timeout behavior
5. Test concurrent command execution
6. Memory leak testing
7. Performance profiling

**Success Criteria:**
- All test scenarios pass
- No deadlocks or race conditions
- Memory stable over long sessions
- Response times acceptable

---

## Key Implementation Details

### 1. Handling `^running` Commands

Commands like `-exec-run` and `-exec-continue` are special:

```
Send: 123-exec-continue
Receive: 123^running
Receive: (gdb)          # First prompt - command accepted
         [program running...]
Receive: *stopped,...   # Async event - program stopped
Receive: (gdb)          # Second prompt - ready for next command
```

**Implementation:**
```csharp
if (pendingCommand.ExpectsRunningState)
{
    // Don't complete on first (gdb) after ^running
    // Wait for *stopped event
    pendingCommand.WaitingForStopped = true;
}

// In HandleAsyncExecRecordAsync:
if (line.StartsWith("*stopped") && pendingCommand.WaitingForStopped)
{
    pendingCommand.AccumulatedRecords.Add(line);
    pendingCommand.WaitingForStopped = false;
    // Next (gdb) will complete the command
}
```

### 2. Token Generation Strategy

```csharp
// Thread-safe token generation
private int GetNextToken()
{
    return Interlocked.Increment(ref _nextToken);
}

// Reset on overflow
if (_nextToken > 999999)
{
    Interlocked.Exchange(ref _nextToken, 1);
}
```

### 3. Queue Capacity Management

```csharp
// Bounded queue to prevent memory exhaustion
private const int MaxQueueSize = 10000;

_outputQueues[sessionId] = new BlockingCollection<string>(MaxQueueSize);

// If queue is full, older items are dropped (FIFO)
if (queue.Count >= MaxQueueSize)
{
    queue.TryTake(out _); // Drop oldest
    logger.LogWarning("Output queue full for session {SessionId}, dropping old records", sessionId);
}
```

### 4. Graceful Shutdown Sequence

```csharp
public async Task DisconnectAsync(string sessionId)
{
    // 1. Cancel background reader
    if (_readerCancellations.TryGetValue(sessionId, out var cts))
    {
        cts.Cancel();
    }
    
    // 2. Wait for reader to stop (with timeout)
    if (_outputReaders.TryGetValue(sessionId, out var readerTask))
    {
        await Task.WhenAny(readerTask, Task.Delay(5000));
    }
    
    // 3. Fail pending commands
    if (_pendingCommands.TryGetValue(sessionId, out var commands))
    {
        foreach (var cmd in commands.Values)
        {
            cmd.CompletionSource.TrySetException(
                new OperationCanceledException("Session disconnected")
            );
        }
    }
    
    // 4. Clean up resources
    // [streams, process, etc.]
}
```

---

## Testing Strategy

### Unit Tests

1. **Token Matching**
    - Verify correct response goes to correct command
    - Test out-of-order responses
    - Test missing responses (timeout)

2. **Queue Management**
    - Test queue capacity limits
    - Test concurrent producers/consumers
    - Test queue disposal

3. **Event Distribution**
    - Verify events fire for all async records
    - Test event parsing
    - Test thread safety of event handlers

### Integration Tests

1. **Real Debugging Scenarios**
    - Launch, set breakpoint, run, hit breakpoint, continue, exit
    - Multiple breakpoints
    - Step through code
    - Evaluate expressions at breakpoint

2. **Error Scenarios**
    - Process crashes during execution
    - Invalid commands
    - Timeouts
    - Connection lost

3. **Performance Tests**
    - 1000 commands in rapid succession
    - Long-running session (hours)
    - Memory leak detection
    - CPU usage profiling

---

## Migration Path from Current Code

### Step 1: Add New Infrastructure (Non-Breaking)
- Add new fields for background reader
- Keep existing synchronous code working

### Step 2: Dual-Mode Operation (Transition)
- Add flag to enable new async reader
- Run both modes in parallel for testing
- Compare results

### Step 3: Switch Default (Breaking)
- Make async mode the default
- Mark old code as deprecated

### Step 4: Remove Old Code (Cleanup)
- Delete synchronous ReadResponseAsync
- Remove deprecated fields

---

## Open Questions / Decisions Needed

1. **Event Buffering**
    - Should events be queued if no subscribers?
    - How long to buffer events?
    - Drop old events or block?

2. **Command Serialization**
    - Allow concurrent commands or serialize?
    - Per-session or global lock?
    - Priority queue for commands?

3. **Stream Output Handling**
    - Buffer console output (`~"..."`) separately?
    - Forward to MCP tool immediately?
    - Include in command response or separate event?

4. **Performance Tuning**
    - Queue size limits
    - Background reader thread priority
    - Batching of events

5. **Logging Level**
    - All MI records at Trace level?
    - Important events at Information level?
    - Errors at Error level?

---

## Success Metrics

After implementation, the system should:

1. ✅ Handle all MI protocol scenarios without data loss
2. ✅ Support concurrent command execution
3. ✅ Fire events for all async notifications
4. ✅ Complete commands with correct responses
5. ✅ Timeout commands that don't respond
6. ✅ Gracefully handle process crashes
7. ✅ Run for hours without memory leaks
8. ✅ Process 100+ commands/second
9. ✅ No deadlocks or race conditions
10. ✅ Clean shutdown of all sessions

---

## References

- MI Protocol Flow Documentation: `MI_PROTOCOL_FLOW.md`
- GDB/MI Documentation: https://sourceware.org/gdb/current/onlinedocs/gdb/GDB_002fMI.html
- netcoredbg GitHub: https://github.com/Samsung/netcoredbg
- DAP Specification: https://microsoft.github.io/debug-adapter-protocol/

---

**Last Updated:** 2025-10-27  
**Status:** Design Phase - Ready for Implementation  
**Priority:** CRITICAL - Current implementation is fundamentally broken