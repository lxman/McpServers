# Command Execution

Execute shell commands with session management and persistent terminal state.

**See [../COMMON.md](../COMMON.md) for shared concepts: sessionId, standard responses, security (blocked commands).**

---

## Tools Quick Reference

| Tool | Purpose | Key Parameters |
|------|---------|----------------|
| [execute_command](execute_command.md) | Execute shell command | command, sessionId?, workingDirectory?, timeoutMs? |
| [send_input](send_input.md) | Send input to session | sessionId, input |
| [get_session_output](get_session_output.md) | Get session output buffer | sessionId? |
| [list_sessions](list_sessions.md) | List active sessions | (none) |
| [close_session](close_session.md) | Close terminal session | sessionId |

---

## Session Concepts

### What is a Session?

A session is a persistent terminal instance that maintains:
- **Working directory:** Current folder context
- **Environment variables:** Custom PATH, settings
- **Process state:** Running applications
- **Output buffer:** Command results

### Session Lifecycle

```
1. execute_command with sessionId: "build"
   → Creates session if doesn't exist
   → Runs command in that session

2. execute_command with same sessionId
   → Reuses existing session
   → Maintains working directory

3. close_session(sessionId: "build")
   → Terminates session
   → Cleans up resources
```

### Default Session

- **ID:** "default"
- **Behavior:** Auto-created on first use
- **Working directory:** Initial server directory
- **Use case:** One-off commands

---

## Common Workflows

### Single Command Execution
```
execute_command(command: "npm --version")
→ Runs in default session
→ Returns output immediately
```

### Multi-Step Build Process
```
1. execute_command(
     command: "cd C:\\Projects\\MyApp",
     sessionId: "build"
   )

2. execute_command(
     command: "npm install",
     sessionId: "build"
   )

3. execute_command(
     command: "npm run build",
     sessionId: "build"
   )

4. close_session(sessionId: "build")
```

### Interactive Application
```
1. execute_command(
     command: "python interactive.py",
     sessionId: "python",
     timeoutMs: 60000
   )

2. send_input(
     sessionId: "python",
     input: "user input\n"
   )

3. get_session_output(sessionId: "python")
   → Retrieve application output

4. Repeat steps 2-3 for interaction

5. send_input(sessionId: "python", input: "exit\n")
6. close_session(sessionId: "python")
```

### Development Server
```
1. execute_command(
     command: "npm run dev",
     sessionId: "dev-server"
   )
   → Server starts, continues running

2. Do other work...

3. close_session(sessionId: "dev-server")
   → Stops server
```

---

## Command Execution Details

### Working Directory

**Set initially:**
```
execute_command(
  command: "dir",
  workingDirectory: "C:\\Projects\\MyApp"
)
→ Runs in specified directory
```

**Changes persist in session:**
```
Session "build":
  1. execute_command(command: "cd src")
     → Working directory now: C:\\Projects\\MyApp\\src
  
  2. execute_command(command: "dir")
     → Lists files in src directory
```

### Timeout

- **Default:** 30 seconds (30000ms)
- **Configurable:** Set timeoutMs parameter
- **Behavior:** Command killed if exceeds timeout
- **Long-running:** Use high timeout or start as background

**Examples:**
```
Quick command:
  timeoutMs: 5000 (5 seconds)

Build process:
  timeoutMs: 300000 (5 minutes)

Dev server:
  timeoutMs: -1 (no timeout, runs indefinitely)
```

### Output Capture

**Immediate:**
```
result = execute_command(command: "echo Hello")
→ result.output = "Hello\n"
→ result.exitCode = 0
```

**Buffered (for interactive):**
```
1. execute_command(command: "long-running-app")
2. get_session_output() periodically
   → Retrieve accumulated output
```

---

## Best Practices

### Session Management

1. **Use named sessions for related commands:**
   ```
   ✓ Good: sessionId: "build", "test", "deploy"
   ✗ Bad: Reusing "default" for everything
   ```

2. **Always close sessions when done:**
   ```
   try {
     execute_command(...)
     execute_command(...)
   } finally {
     close_session(sessionId)
   }
   ```

3. **Check active sessions:**
   ```
   list_sessions()
   → See what's still running
   → Close unused sessions
   ```

### Command Safety

1. **Check blocked commands:**
    - See [security-config/test_command_blocking](../security-config/test_command_blocking.md)
    - Test before execution if unsure
    - Blocked commands fail immediately

2. **Avoid destructive commands:**
    - Format, delete system files, etc.
    - Configure blocked commands appropriately
    - Use confirmation for dangerous operations

3. **Handle timeouts gracefully:**
    - Set reasonable timeouts
    - Handle timeout errors
    - Don't assume command succeeded

### Error Handling

1. **Check exit codes:**
   ```
   result = execute_command(command: "npm build")
   if result.exitCode != 0:
       handle_build_failure(result.output)
   ```

2. **Parse stderr:**
    - Errors typically go to stderr
    - Check for error indicators in output
    - Log for troubleshooting

3. **Command not found:**
    - Verify command is in PATH
    - Use full path if needed
    - Check spelling

---

## Common Use Cases

### Build Automation
```
sessionId: "build"
1. cd to project directory
2. npm install
3. npm run lint
4. npm run test
5. npm run build
6. Close session
```

### Git Operations
```
sessionId: "git"
1. git status
2. git add .
3. git commit -m "message"
4. git push
5. Close session
```

### Docker Management
```
sessionId: "docker"
1. docker build -t myapp .
2. docker run -p 8080:80 myapp
3. (Server runs)
4. Close session (stops container)
```

### Database Operations
```
sessionId: "db"
1. psql -U user -d database
2. send_input: SQL query
3. get_session_output: Results
4. send_input: \q (quit)
5. Close session
```

---

## Security Considerations

### Command Blocking

**Configured in security-config:**
```
Blocked patterns:
  - "format"
  - "del /s /q C:\\*"
  - "rm -rf /"
  - "shutdown"
```

**Enforcement:**
```
execute_command(command: "format c:")
→ Error: "Command is blocked by security policy"
```

### Input Validation

1. **Sanitize user input:**
    - Validate before passing to commands
    - Prevent command injection
    - Escape special characters

2. **Don't trust external data:**
    - Validate file paths
    - Check command arguments
    - Use parameterized approaches when possible

### Audit Logging

All command executions logged:
- Command text
- Session ID
- Exit code
- Execution time
- Success/failure

Review with [maintenance/get_audit_log](../maintenance/get_audit_log.md)

---

## Performance Tips

1. **Reuse sessions:**
    - Avoid creating/destroying frequently
    - Share session for related commands
    - Reduces overhead

2. **Appropriate timeouts:**
    - Don't use excessively long timeouts
    - Set based on expected duration
    - Monitor long-running commands

3. **Output buffering:**
    - For verbose commands, get output periodically
    - Prevent memory buildup
    - Clear buffer after processing

---

## Troubleshooting

### Command Not Found
```
Issue: "command not found" or "'command' is not recognized"
Solutions:
  - Use full path: C:\\Program Files\\...\\tool.exe
  - Verify PATH environment variable
  - Check spelling and case
```

### Permission Denied
```
Issue: "Access denied" or permission errors
Solutions:
  - Run Desktop Commander as administrator
  - Check file/directory permissions
  - Verify user has necessary rights
```

### Timeout Exceeded
```
Issue: Command times out before completing
Solutions:
  - Increase timeoutMs parameter
  - Check if command is stuck
  - Run in background if appropriate
```

### Session Not Found
```
Issue: "Session not found" error
Solutions:
  - Session was closed
  - Session ID typo
  - Use list_sessions() to verify
```

### Output Not Appearing
```
Issue: get_session_output returns empty
Solutions:
  - Command may not have finished
  - Output might be going to file instead of stdout
  - Check if command has output at all
```

---

## Advanced Patterns

### Parallel Execution
```
Session "task1": execute_command(...)
Session "task2": execute_command(...)
Session "task3": execute_command(...)

Monitor all sessions:
  - get_session_output for each
  - Check completion status
  - Collect results
```

### Pipeline Commands
```
In session:
  command: "command1 | command2 | command3"
  → Shell handles pipeline
```

### Background Processes
```
Windows:
  command: "start /B long-running-app"

Linux:
  command: "nohup long-running-app &"
```

---

**Total Tools:** 5  
**See [../INDEX.md](../INDEX.md) for complete Desktop Commander reference**