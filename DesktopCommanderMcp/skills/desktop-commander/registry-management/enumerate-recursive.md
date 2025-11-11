# enumerate_registry_keys_recursive

Enumerate all subkeys recursively under a Windows Registry key with optional depth limit.

**Category:** [registry-management](INDEX.md)

---

## Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| path | string | ✓ | - | Registry key path to enumerate |
| maxDepth | integer | ✗ | 0 | Maximum depth (0 = unlimited) |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| path | string | Registry key path |
| maxDepth | integer | Maximum depth used |
| keyCount | integer | Total number of keys found |
| keys | string[] | Array of full key paths |

**Example response:**
```json
{
  "success": true,
  "path": "HKEY_CURRENT_USER\\Software\\MyApp",
  "maxDepth": 2,
  "keyCount": 15,
  "keys": [
    "HKEY_CURRENT_USER\\Software\\MyApp\\Config",
    "HKEY_CURRENT_USER\\Software\\MyApp\\Config\\Advanced",
    "HKEY_CURRENT_USER\\Software\\MyApp\\Data",
    "..."
  ]
}
```

---

## Examples

**Enumerate all subkeys (unlimited depth):**
```
enumerate_registry_keys_recursive(
  path: "HKEY_CURRENT_USER\\Software\\MyApp"
)
→ Returns all nested subkeys
```

**Enumerate with depth limit:**
```
enumerate_registry_keys_recursive(
  path: "HKEY_LOCAL_MACHINE\\SOFTWARE",
  maxDepth: 2
)
→ Limits recursion to 2 levels deep
```

**Quick structure overview:**
```
enumerate_registry_keys_recursive(
  path: "HKEY_CURRENT_USER\\Software\\Microsoft",
  maxDepth: 1
)
→ Shows only direct children
```

---

## Notes

- **Recursive:** Traverses entire subtree
- **Depth control:** Use maxDepth to limit recursion
- **Performance:** Large trees can take time and return many results
- **Permissions:** Requires read access to all enumerated keys
- **Audit log:** Operation is logged for security tracking
- **Use case:** Registry exploration, backup, structure analysis
- **Full paths:** Returns complete registry paths, not just names

---

## Related Tools

- [list_registry_subkeys](list-subkeys.md) - List direct children only
- [get_registry_key_info](get-key-info.md) - Get single-level key info
- [check_registry_key_exists](check-key-exists.md) - Check if key exists
