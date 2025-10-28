# get_session_output

Get accumulated output from a session buffer.

**Category:** [command-execution](INDEX.md)

---

## Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| sessionId | string | ✗ | "default" | Session identifier |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| sessionId | string | Session ID |
| output | string | Accumulated output |
| outputLength | integer | Length of output |

---

## Example

```
1. execute_command(command: "long-running-app", sessionId: "app")
2. get_session_output(sessionId: "app")
   → Get output so far
3. Wait...
4. get_session_output(sessionId: "app")
   → Get more output
```

---

## Notes

- **Buffered:** Output accumulates in session buffer
- **Use case:** Monitor long-running processes, interactive applications
- **Clear:** Output buffer not cleared after reading

---

## Related Tools

- [execute_command](execute_command.md) - Start session
- [send_input](send_input.md) - Send input
- [close_session](close_session.md) - Close session