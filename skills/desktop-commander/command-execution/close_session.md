# close_session

Close a command execution session and cleanup resources.

**Category:** [command-execution](INDEX.md)

---

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| sessionId | string | ✓ | Session identifier to close |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| sessionId | string | Session that was closed |
| closed | boolean | Whether session was closed |

---

## Example

```
close_session(sessionId: "build")
→ Terminates session and cleans up
```

---

## Notes

- **Cleanup:** Terminates any running processes in session
- **Resources:** Releases memory and handles
- **Default session:** Can be closed but auto-recreates on next use
- **Best practice:** Always close sessions when done

---

## Related Tools

- [execute_command](execute_command.md) - Creates sessions
- [list_sessions](list_sessions.md) - See active sessions