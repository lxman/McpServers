# test_directory_access

Test if a directory is in the allowed list.

**Category:** [security-config](INDEX.md)

---

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| directoryPath | string | ✓ | Full path to test |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| directoryPath | string | Path that was tested |
| isAllowed | boolean | Whether access is allowed |
| matchedPath | string | Allowed parent path (if allowed) |

---

## Examples

**Test access:**
```
test_directory_access(directoryPath: "C:\\Projects\\MyApp")
→ {
    "isAllowed": true,
    "matchedPath": "C:\\Projects"
  }
```

**Test denied access:**
```
test_directory_access(directoryPath: "C:\\Windows\\System32")
→ {
    "isAllowed": false
  }
```

---

## Notes

- **Before operations:** Test before write/delete operations
- **Subdirectories:** Allowed if any parent is allowed
- **Add if needed:** Use [add_allowed_directory](add_allowed_directory.md) if denied

---

## Related Tools

- [get_configuration](get_configuration.md) - View allowed directories
- [add_allowed_directory](add_allowed_directory.md) - Grant access