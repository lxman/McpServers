# write_registry_value

Write a value to Windows Registry.

**Category:** [registry-management](INDEX.md)

---

## Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| path | string | ✓ | - | Registry key path |
| valueName | string | ✓ | - | Name of the value to write |
| value | string | ✓ | - | Value to write (string representation) |
| valueType | string | ✗ | String | Type: String, DWord, QWord, Binary, MultiString, ExpandString |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| path | string | Registry key path |
| valueName | string | Name of the value |
| value | string | The written value |
| valueType | string | Registry value type |

---

## Examples

**Write a string value:**
```
write_registry_value(
  path: "HKEY_CURRENT_USER\\Software\\MyApp",
  valueName: "Version",
  value: "2.0.0",
  valueType: "String"
)
```

**Write a DWORD value:**
```
write_registry_value(
  path: "HKEY_CURRENT_USER\\Software\\MyApp\\Settings",
  valueName: "MaxConnections",
  value: "100",
  valueType: "DWord"
)
```

**Write binary data (hex string):**
```
write_registry_value(
  path: "HKEY_LOCAL_MACHINE\\SOFTWARE\\MyApp",
  valueName: "BinaryData",
  value: "48656C6C6F",
  valueType: "Binary"
)
```

**Write multi-string value:**
```
write_registry_value(
  path: "HKEY_CURRENT_USER\\Software\\MyApp",
  valueName: "SearchPaths",
  value: "C:\\Path1\nC:\\Path2\nC:\\Path3",
  valueType: "MultiString"
)
→ Use \n to separate strings
```

---

## Notes

- **Permissions:** Requires write access to the registry key
- **Value conversion:**
  - DWord: Parsed as 32-bit integer
  - QWord: Parsed as 64-bit integer
  - Binary: Converted from hex string
  - MultiString: Split by newline (\n)
- **Key creation:** Automatically creates parent keys if needed
- **Audit log:** All write operations are logged for security tracking
- **Use case:** Configuration management, application settings

---

## Related Tools

- [read_registry_value](read-value.md) - Read a registry value
- [delete_registry_value](delete-value.md) - Delete a registry value
- [create_registry_key](create-key.md) - Create a registry key
