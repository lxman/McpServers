# add_blocked_command

Add command pattern to blocked list (blacklist).

**Category:** [security-config](INDEX.md)

---

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| commandPattern | string | ✓ | Command pattern to block (supports wildcards) |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| commandPattern | string | Pattern that was added |
| added | boolean | Whether pattern was added |
| alreadyBlocked | boolean | Was already in list |

---

## Examples

**Block format command:**
```
add_blocked_command(commandPattern: "format")
→ Blocks "format", "format c:", "FORMAT D:", etc.
```

**Block destructive delete:**
```
add_blocked_command(commandPattern: "del /s /q C:\\*")
```

**Block wildcards:**
```
add_blocked_command(commandPattern: "rm -rf *")
```

---

## Notes

- **Case-insensitive:** Pattern matches regardless of case
- **Substring matching:** "format" blocks "format c:" and "FORMAT"
- **Wildcards:** Use `*` for any characters
- **Test first:** Use [test_command_blocking](test_command_blocking.md) to verify

---

## Related Tools

- [get_configuration](get_configuration.md) - View blocked commands
- [test_command_blocking](test_command_blocking.md) - Test pattern