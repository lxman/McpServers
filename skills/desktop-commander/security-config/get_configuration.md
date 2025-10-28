# get_configuration

Get current security configuration (allowed directories and blocked commands).

**Category:** [security-config](INDEX.md)

---

## Parameters

None

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| allowedDirectories | string[] | Whitelisted directory paths |
| blockedCommands | string[] | Blacklisted command patterns |
| totalAllowedDirectories | integer | Count of allowed directories |
| totalBlockedCommands | integer | Count of blocked commands |
| configPath | string | Path to configuration file |
| lastModified | string | Last modification time |

---

## Example

```
get_configuration()
→ {
    "allowedDirectories": ["C:\\Projects", "C:\\Temp"],
    "blockedCommands": ["format", "del /s /q C:\\*"],
    ...
  }
```

---

## Notes

- **Use case:** Review security settings, compliance audits
- **First step:** Check configuration before operations
- **Regular review:** Audit directories and commands periodically

---

## Related Tools

- [add_allowed_directory](add_allowed_directory.md) - Grant access
- [add_blocked_command](add_blocked_command.md) - Block command
- [test_directory_access](test_directory_access.md) - Test access
- [test_command_blocking](test_command_blocking.md) - Test blocking