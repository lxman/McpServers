# create_registry_key

Create a new Windows Registry key.

**Category:** [registry-management](INDEX.md)

---

## Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| path | string | ✓ | - | Registry key path to create |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| path | string | Registry key path |
| created | boolean | True if key was created |

**Example response:**
```json
{
  "success": true,
  "path": "HKEY_CURRENT_USER\\Software\\MyApp\\Settings",
  "created": true
}
```

---

## Examples

**Create a new key:**
```
create_registry_key(
  path: "HKEY_CURRENT_USER\\Software\\MyApp"
)
→ Creates the key if it doesn't exist
```

**Create nested structure:**
```
create_registry_key(
  path: "HKEY_CURRENT_USER\\Software\\MyApp\\Settings\\Advanced"
)
→ Automatically creates parent keys
```

---

## Notes

- **Permissions:** Requires write access to parent key
- **Parent creation:** Automatically creates parent keys if needed
- **Idempotent:** Returns error if key already exists
- **Audit log:** Operation is logged for security tracking
- **Use case:** Application setup, configuration initialization

---

## Related Tools

- [check_registry_key_exists](check-key-exists.md) - Check if key exists before creation
- [delete_registry_key](delete-key.md) - Delete a registry key
- [write_registry_value](write-value.md) - Write values to the key
