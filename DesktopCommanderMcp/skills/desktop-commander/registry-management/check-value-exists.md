# check_registry_value_exists

Check if a Windows Registry value exists.

**Category:** [registry-management](INDEX.md)

---

## Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| path | string | ✓ | - | Registry key path |
| valueName | string | ✓ | - | Name of the value to check |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| path | string | Registry key path |
| valueName | string | Name of the value |
| exists | boolean | True if value exists, false otherwise |

**Example response:**
```json
{
  "success": true,
  "path": "HKEY_LOCAL_MACHINE\\SOFTWARE\\MyApp",
  "valueName": "Version",
  "exists": true
}
```

---

## Examples

**Check if value exists:**
```
check_registry_value_exists(
  path: "HKEY_LOCAL_MACHINE\\SOFTWARE\\MyApp",
  valueName: "Version"
)
→ Returns exists: true/false
```

**Before reading a value:**
```
check_registry_value_exists(
  path: "HKEY_CURRENT_USER\\Software\\MyApp\\Settings",
  valueName: "LastRun"
)
→ Check before reading to avoid errors
```

---

## Notes

- **Safe operation:** Does not modify the registry
- **Permissions:** Requires read access to the key
- **Use case:** Validation before operations, conditional logic
- **Key must exist:** If the key doesn't exist, returns error

---

## Related Tools

- [check_registry_key_exists](check-key-exists.md) - Check if key exists
- [read_registry_value](read-value.md) - Read a registry value
- [list_registry_values](list-values.md) - List all values in a key
