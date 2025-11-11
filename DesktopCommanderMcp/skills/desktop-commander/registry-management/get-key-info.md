# get_registry_key_info

Get detailed information about a Windows Registry key including subkeys and values.

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
| path | string | Full registry key path |
| name | string | Key name (last component) |
| subKeyCount | integer | Number of subkeys |
| valueCount | integer | Number of values |
| subKeyNames | string[] | Array of subkey names |
| valueNames | string[] | Array of value names |

**Example response:**
```json
{
  "success": true,
  "path": "HKEY_LOCAL_MACHINE\\SOFTWARE\\MyApp",
  "name": "MyApp",
  "subKeyCount": 3,
  "valueCount": 5,
  "subKeyNames": ["Config", "Logs", "Data"],
  "valueNames": ["Version", "InstallPath", "Enabled", "Port", "MaxConnections"]
}
```

---

## Examples

**Get key information:**
```
get_registry_key_info(
  path: "HKEY_LOCAL_MACHINE\\SOFTWARE\\MyApp"
)
→ Returns structure with counts and names
```

**Explore key structure:**
```
get_registry_key_info(
  path: "HKEY_CURRENT_USER\\Software"
)
→ View all subkeys and values
```

---

## Notes

- **Permissions:** Requires read access to the key
- **Non-recursive:** Only returns direct children (one level)
- **Audit log:** Operation is logged for security tracking
- **Use case:** Registry exploration, structure analysis
- **Key must exist:** Returns error if key doesn't exist

---

## Related Tools

- [list_registry_subkeys](list-subkeys.md) - List only subkeys
- [list_registry_values](list-values.md) - List only values
- [enumerate_registry_keys_recursive](enumerate-recursive.md) - Recursive enumeration
