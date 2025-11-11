# delete_registry_key

Delete a Windows Registry key.

**Category:** [registry-management](INDEX.md)

---

## Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| path | string | ✓ | - | Registry key path to delete |
| recursive | boolean | ✗ | false | If true, deletes key with all subkeys |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| path | string | Registry key path |
| deleted | boolean | True if key was deleted |
| recursive | boolean | Whether recursive deletion was used |

**Example response:**
```json
{
  "success": true,
  "path": "HKEY_CURRENT_USER\\Software\\MyApp",
  "deleted": true,
  "recursive": true
}
```

---

## Examples

**Delete an empty key:**
```
delete_registry_key(
  path: "HKEY_CURRENT_USER\\Software\\MyApp\\Temp"
)
→ Deletes key if it has no subkeys
```

**Delete key with subkeys:**
```
delete_registry_key(
  path: "HKEY_CURRENT_USER\\Software\\MyApp",
  recursive: true
)
→ Deletes key and all its subkeys
```

---

## Notes

- **Permissions:** Requires write access to parent key
- **Recursive required:** Non-recursive delete fails if key has subkeys
- **Audit log:** Operation is logged for security tracking
- **Irreversible:** Cannot be undone - use with caution
- **Use case:** Cleanup, uninstallation, configuration reset
- **Safety:** Consider checking key contents before recursive delete

---

## Related Tools

- [check_registry_key_exists](check-key-exists.md) - Check if key exists
- [get_registry_key_info](get-key-info.md) - Inspect key before deletion
- [create_registry_key](create-key.md) - Create a registry key
