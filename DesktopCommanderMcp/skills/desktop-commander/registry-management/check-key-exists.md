# check_registry_key_exists

Check if a Windows Registry key exists.

**Category:** [registry-management](INDEX.md)

---

## Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| path | string | ✓ | - | Registry key path to check |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| path | string | Registry key path |
| exists | boolean | True if key exists, false otherwise |

**Example response:**
```json
{
  "success": true,
  "path": "HKEY_LOCAL_MACHINE\\SOFTWARE\\MyApp",
  "exists": true
}
```

---

## Examples

**Check if key exists:**
```
check_registry_key_exists(
  path: "HKEY_LOCAL_MACHINE\\SOFTWARE\\MyApp"
)
→ Returns exists: true/false
```

**Before creating a key:**
```
check_registry_key_exists(
  path: "HKEY_CURRENT_USER\\Software\\NewApp"
)
→ Check before creation to avoid errors
```

---

## Notes

- **Safe operation:** Does not modify the registry
- **Permissions:** Requires read access to the key
- **Use case:** Validation before operations, conditional logic
- **No side effects:** Does not create or modify keys

---

## Related Tools

- [check_registry_value_exists](check-value-exists.md) - Check if value exists
- [create_registry_key](create-key.md) - Create a registry key
- [get_registry_key_info](get-key-info.md) - Get detailed key information
