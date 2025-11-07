# add_allowed_directory

Add directory to allowed list (whitelist).

**Category:** [security-config](INDEX.md)

---

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| directoryPath | string | ✓ | Full path to directory |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| directoryPath | string | Path that was added |
| added | boolean | Whether directory was added |
| alreadyAllowed | boolean | Was already in list |

---

## Examples

**Grant access to project:**
```
add_allowed_directory(directoryPath: "C:\\Projects\\MyApp")
```

**Grant access to user documents:**
```
add_allowed_directory(directoryPath: "C:\\Users\\username\\Documents")
```

---

## Notes

- **Subdirectories included:** All subdirectories automatically allowed
- **Write/delete operations:** Now permitted in this directory
- **Persisted:** Saved to configuration file
- **Verification:** Use [test_directory_access](test_directory_access.md) to verify

---

## Related Tools

- [get_configuration](get_configuration.md) - View current settings
- [test_directory_access](test_directory_access.md) - Test access