# delete

Delete a file or directory.

**Category:** [file-operations](INDEX.md)  
**Security:** [Requires allowed directory](../COMMON.md#security-model)

---

## Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| path | string | ✓ | - | Full path to delete |
| force | boolean | ✗ | false | Force delete directories with contents |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| path | string | Path that was deleted |
| deleted | boolean | Whether deletion succeeded |

---

## Examples

**Delete file:**
```
delete(path: "C:\Temp\old-file.txt")
```

**Delete empty directory:**
```
delete(path: "C:\Temp\EmptyFolder")
```

**Delete directory with contents:**
```
delete(
  path: "C:\Temp\FolderWithFiles",
  force: true
)
→ Recursively deletes all contents
```

---

## Notes

- **Security:** Path must be in allowed directory
- **Irreversible:** No recycle bin, permanent deletion
- **Non-empty directories:** Require `force: true`
- **Use with caution:** Double-check paths before deleting
- **Validation:** Always verify path before calling

---

## Related Tools

- [move](move.md) - Move instead of delete
- [list_directory](list_directory.md) - Check contents first
- [get_file_info](get_file_info.md) - Verify path exists