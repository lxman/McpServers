# list_registry_subkeys

List all subkeys under a Windows Registry key.

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
| subKeyCount | integer | Number of subkeys |
| subKeys | string[] | Array of subkey names |

**Example response:**
```json
{
  "success": true,
  "path": "HKEY_LOCAL_MACHINE\\SOFTWARE",
  "subKeyCount": 125,
  "subKeys": ["Microsoft", "Classes", "Policies", "...]
}
```

---

## Examples

**List all subkeys:**
```
list_registry_subkeys(
  path: "HKEY_LOCAL_MACHINE\\SOFTWARE"
)
→ Returns all direct child keys
```

**Navigate registry structure:**
```
list_registry_subkeys(
  path: "HKEY_CURRENT_USER\\Software\\Microsoft"
)
```

---

## Notes

- **Non-recursive:** Only returns direct children (one level)
- **Sorted:** Typically alphabetically by name
- **Permissions:** Requires read access to the key
- **Use case:** Registry navigation, structure exploration

---

## Related Tools

- [list_registry_values](list-values.md) - List values in a key
- [get_registry_key_info](get-key-info.md) - Get key info with both subkeys and values
- [enumerate_registry_keys_recursive](enumerate-recursive.md) - Recursive enumeration
