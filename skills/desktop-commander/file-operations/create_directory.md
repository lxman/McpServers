# create_directory

Create a new directory.

**Category:** [file-operations](INDEX.md)  
**Security:** [Requires allowed directory](../COMMON.md#security-model)

---

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| path | string | ✓ | Full path to directory to create |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| path | string | Absolute path created |
| created | boolean | Whether directory was newly created |

---

## Examples

```
create_directory(path: "C:\Projects\NewApp")

create_directory(path: "C:\Projects\MyApp\src\components")
→ Creates nested directories if needed
```

---

## Notes

- **Security:** Path must be within [allowed directories](../security-config/INDEX.md)
- **Parent creation:** Creates parent directories automatically if needed
- **Already exists:** Returns success if directory already exists
- **Use case:** Project setup, organizing files

---

## Related Tools

- [list_directory](list_directory.md) - Verify creation
- [delete](delete.md) - Remove directory