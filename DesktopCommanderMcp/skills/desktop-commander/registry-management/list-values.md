# list_registry_values

List all values in a Windows Registry key with their data and types.

**Category:** [registry-management](INDEX.md)

---

## Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| path | string | ✓ | - | Registry key path |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| path | string | Registry key path |
| valueCount | integer | Number of values |
| values | object[] | Array of value objects |

**Value object:**
```json
{
  "name": "Version",
  "value": "1.0.0",
  "valueType": "String"
}
```

**Example response:**
```json
{
  "success": true,
  "path": "HKEY_LOCAL_MACHINE\\SOFTWARE\\MyApp",
  "valueCount": 3,
  "values": [
    {"name": "Version", "value": "1.0.0", "valueType": "String"},
    {"name": "Enabled", "value": 1, "valueType": "DWord"},
    {"name": "InstallPath", "value": "C:\\Program Files\\MyApp", "valueType": "String"}
  ]
}
```

---

## Examples

**List all values:**
```
list_registry_values(
  path: "HKEY_LOCAL_MACHINE\\SOFTWARE\\MyApp"
)
→ Returns all values with names, data, and types
```

**Inspect configuration:**
```
list_registry_values(
  path: "HKEY_CURRENT_USER\\Software\\MyApp\\Settings"
)
```

---

## Notes

- **Complete data:** Returns value names, data, and types
- **Permissions:** Requires read access to the key
- **Value types:** String, DWord, QWord, Binary, MultiString, ExpandString
- **Use case:** Configuration inspection, backup, migration

---

## Related Tools

- [read_registry_value](read-value.md) - Read a specific value
- [list_registry_subkeys](list-subkeys.md) - List subkeys
- [get_registry_key_info](get-key-info.md) - Get key info with value names only
