# test_command_blocking

Test if a command is blocked by security policy.

**Category:** [security-config](INDEX.md)

---

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| command | string | ✓ | Command to test |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| command | string | Command that was tested |
| isBlocked | boolean | Whether command is blocked |
| matchedPattern | string | Blocking pattern (if blocked) |

---

## Examples

**Test safe command:**
```
test_command_blocking(command: "npm install")
→ {
    "isBlocked": false
  }
```

**Test blocked command:**
```
test_command_blocking(command: "format c:")
→ {
    "isBlocked": true,
    "matchedPattern": "format"
  }
```

---

## Notes

- **Before execution:** Test before [execute_command](../command-execution/execute_command.md)
- **Case-insensitive:** Matching ignores case
- **Add if needed:** Use [add_blocked_command](add_blocked_command.md) to block commands

---

## Related Tools

- [get_configuration](get_configuration.md) - View blocked commands
- [add_blocked_command](add_blocked_command.md) - Block commands
- [execute_command](../command-execution/execute_command.md) - Execute if allowed