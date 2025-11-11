# read_registry_value

Read a value from Windows Registry.

**Category:** [registry-management](INDEX.md)

---

## Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| path | string | ✓ | - | Registry key path (e.g., HKEY_LOCAL_MACHINE\SOFTWARE\MyApp) |
| valueName | string | ✓ | - | Name of the value to read |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| path | string | Registry key path |
| valueName | string | Name of the value |
| value | object | The value data |
| valueType | string | Registry value type |

**Example response:**
```json
{
  "success": true,
  "path": "HKEY_LOCAL_MACHINE\\SOFTWARE\\MyApp",
  "valueName": "Version",
  "value": "1.0.0",
  "valueType": "String"
}
```

---

## Examples

**Read a string value:**
```
read_registry_value(
  path: "HKEY_LOCAL_MACHINE\\SOFTWARE\\MyApp",
  valueName: "Version"
)
```

**Read a DWORD value:**
```
read_registry_value(
  path: "HKEY_CURRENT_USER\\Software\\MyApp\\Settings",
  valueName: "Enabled"
)
→ Returns integer value
```

---

## Notes

- **Registry access:** Requires appropriate permissions for the key
- **Value types:** Supports String, DWord, QWord, Binary, MultiString, ExpandString
- **Error handling:** Returns error if key or value doesn't exist
- **Audit log:** All read operations are logged for security tracking
- **Read-only mode:** Uses safe read-only registry access

---

## Related Tools

- [write_registry_value](write-value.md) - Write a registry value
- [check_registry_value_exists](check-value-exists.md) - Check if value exists
- [list_registry_values](list-values.md) - List all values in a key
