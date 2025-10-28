# execute_command

Execute shell command in a session.

**Category:** [command-execution](INDEX.md)  
**Common concepts:** [sessionId](../COMMON.md#sessionid), [security (blocked commands)](../COMMON.md#security-model)

---

## Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| command | string | ✓ | - | Shell command to execute |
| sessionId | string | ✗ | "default" | Session identifier |
| workingDirectory | string | ✗ | null | Working directory for command |
| timeoutMs | integer | ✗ | 30000 | Timeout in milliseconds |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| output | string | Command output (stdout + stderr) |
| exitCode | integer | Exit code (0 = success) |
| executionTime | integer | Execution time in ms |
| sessionId | string | Session used |

---

## Examples

**Simple command:**
```
execute_command(command: "npm --version")
→ "8.19.2"
```

**In specific session:**
```
execute_command(
  command: "npm install",
  sessionId: "build",
  workingDirectory: "C:\\Projects\\MyApp",
  timeoutMs: 120000
)
```

**Multi-step in session:**
```
1. execute_command(command: "cd C:\\Projects\\MyApp", sessionId: "build")
2. execute_command(command: "npm install", sessionId: "build")
3. execute_command(command: "npm run build", sessionId: "build")
→ Working directory persists across commands
```

---

## Notes

- **Sessions:** See [../COMMON.md#sessionid](../COMMON.md#sessionid) for session concepts
- **Security:** Command checked against [blocked commands](../security-config/INDEX.md)
- **Exit codes:** 0 = success, non-zero = error
- **Timeout:** Command killed if exceeds timeout

---

## Related Tools

- [send_input](send_input.md) - Send input to running command
- [get_session_output](get_session_output.md) - Get session output buffer
- [list_sessions](list_sessions.md) - List active sessions
- [close_session](close_session.md) - Close session