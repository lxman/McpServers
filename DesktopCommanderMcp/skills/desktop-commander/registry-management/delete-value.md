# delete_registry_value

Delete a value from a Windows Registry key.

**Category:** [registry-management](INDEX.md)

---

## Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| path | string | ✓ | - | Registry key path |
| valueName | string | ✓ | - | Name of the value to delete |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| path | string | Registry key path |
| valueName | string | Name of the deleted value |
| deleted | boolean | True if value was deleted |

**Example response:**
```json
{
  "success": true,
  "path": "HKEY_CURRENT_USER\\Software\\MyApp",
  "valueName": "TempSetting",
  "deleted": true
}
```

---

## Examples

**Delete a value:**
```
delete_registry_value(
  path: "HKEY_CURRENT_USER\\Software\\MyApp",
  valueName: "TempData"
)
→ Removes the value from the key
```

**Clean up configuration:**
```
delete_registry_value(
  path: "HKEY_LOCAL_MACHINE\\SOFTWARE\\MyApp\\Settings",
  valueName: "ObsoleteSetting"
)
```

---

## Notes

- **Permissions:** Requires write access to the key
- **Key preserved:** Only deletes the value, not the key
- **Audit log:** Operation is logged for security tracking
- **Irreversible:** Cannot be undone - use with caution
- **Error handling:** Returns error if value doesn't exist
- **Use case:** Configuration cleanup, removing obsolete settings

---

## Related Tools

- [check_registry_value_exists](check-value-exists.md) - Check if value exists
- [read_registry_value](read-value.md) - Read value before deletion
- [write_registry_value](write-value.md) - Write a registry value
