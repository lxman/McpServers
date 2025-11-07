# list_directory

List contents of a directory.

**Category:** [file-operations](INDEX.md)  
**Common concepts:** [path parameters](../COMMON.md#path-parameters)

---

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| path | string | ✓ | Full path to directory |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| path | string | Absolute directory path |
| items | object[] | Array of files and subdirectories |
| directoryCount | integer | Number of subdirectories |
| fileCount | integer | Number of files |

**Item structure:**
```json
{
  "name": "file.txt",
  "type": "file",  // or "directory"
  "path": "C:\\full\\path\\file.txt",
  "size": 1024,
  "modified": "2025-10-21T10:00:00Z"
}
```

---

## Examples

**List directory:**
```
list_directory(path: "C:\Projects")
→ Returns all files and subdirectories
```

**Check if directory empty:**
```
result = list_directory(path: "C:\Temp")
if result.fileCount == 0 and result.directoryCount == 0:
    print("Directory is empty")
```

---

## Notes

- **No recursion:** Only lists immediate children (not nested)
- **Sorted:** Items typically sorted alphabetically
- **Hidden files:** Included in results
- **Use case:** File discovery, directory navigation, verification

---

## Related Tools

- [get_file_info](get_file_info.md) - Detailed file/directory metadata
- [search_files](search_files.md) - Recursive file search
- [create_directory](create_directory.md) - Create new directory