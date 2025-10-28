# send_input

Send input to a running session (for interactive applications).

**Category:** [command-execution](INDEX.md)

---

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| sessionId | string | ✓ | Session identifier |
| input | string | ✓ | Input to send (include \n for newline) |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| sessionId | string | Session ID |
| inputSent | string | Input that was sent |

---

## Example

**Interactive Python:**
```
1. execute_command(command: "python", sessionId: "python")
2. send_input(sessionId: "python", input: "print('Hello')\n")
3. get_session_output(sessionId: "python")
   → "Hello"
4. send_input(sessionId: "python", input: "exit()\n")
```

---

## Notes

- **Newlines:** Include \n for line-based input
- **Use case:** Interactive applications, REPLs, prompts
- **Follow-up:** Use [get_session_output](get_session_output.md) to retrieve results

---

## Related Tools

- [execute_command](execute_command.md) - Start session
- [get_session_output](get_session_output.md) - Get output
- [close_session](close_session.md) - Close session