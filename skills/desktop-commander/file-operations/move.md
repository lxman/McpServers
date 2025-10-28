# move

Move or rename a file or directory.

**Category:** [file-operations](INDEX.md)  
**Security:** [Requires allowed directories](../COMMON.md#security-model)

---

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| sourcePath | string | ✓ | Current path |
| destinationPath | string | ✓ | New path |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| sourcePath | string | Original path |
| destinationPath | string | New path |
| moved | boolean | Whether move succeeded |

---

## Examples

**Rename file:**
```
move(
  sourcePath: "C:\Projects\old-name.txt",
  destinationPath: "C:\Projects\new-name.txt"
)
```

**Move to different directory:**
```
move(
  sourcePath: "C:\Temp\file.txt",
  destinationPath: "C:\Projects\MyApp\file.txt"
)
```

**Move directory:**
```
move(
  sourcePath: "C:\Projects\OldFolder",
  destinationPath: "C:\Archive\OldFolder"
)
```

---

## Notes

- **Security:** Both paths must be in allowed directories
- **Atomic:** Operation is atomic
- **Overwrite:** Fails if destination exists
- **Cross-drive:** Works across different drives
- **Use case:** File organization, renaming, restructuring

---

## Related Tools

- [delete](delete.md) - Remove files/directories
- [list_directory](list_directory.md) - Verify move