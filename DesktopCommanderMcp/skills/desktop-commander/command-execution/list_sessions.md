# list_sessions

List all active command execution sessions.

**Category:** [command-execution](INDEX.md)

---

## Parameters

None

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| sessions | object[] | Array of session objects |
| totalSessions | integer | Number of active sessions |

**Session object:**
```json
{
  "sessionId": "build",
  "workingDirectory": "C:\\Projects\\MyApp",
  "createdAt": "2025-10-21T10:00:00Z",
  "lastCommandAt": "2025-10-21T10:05:00Z"
}
```

---

## Example

```
list_sessions()
→ Shows all active sessions: "default", "build", "test"
```

---

## Notes

- **Use case:** Check what's running, cleanup forgotten sessions
- **Default session:** Always exists
- **Cleanup:** Use [close_session](close_session.md) for unused sessions

---

## Related Tools

- [execute_command](execute_command.md) - Create sessions
- [close_session](close_session.md) - Close sessions