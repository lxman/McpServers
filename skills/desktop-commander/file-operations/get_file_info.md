# get_file_info

Get detailed metadata about a file or directory.

**Category:** [file-operations](INDEX.md)

---

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| path | string | ✓ | Full path to file or directory |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| path | string | Absolute path |
| name | string | File/directory name |
| type | string | "file" or "directory" |
| sizeBytes | integer | Size in bytes (files only) |
| created | string | Creation timestamp (ISO 8601) |
| modified | string | Last modified timestamp |
| accessed | string | Last accessed timestamp |
| attributes | string[] | File attributes (readonly, hidden, etc) |
| exists | boolean | Whether path exists |

---

## Example

```
get_file_info(path: "C:\Projects\MyApp\README.md")
→ {
    "name": "README.md",
    "type": "file",
    "sizeBytes": 2048,
    "modified": "2025-10-21T10:00:00Z",
    "attributes": ["archive"]
  }
```

---

## Notes

- **Use case:** Verify file existence, check timestamps, get size
- **Existence check:** Check `exists` field before operations
- **Attributes:** Platform-specific (readonly, hidden, system, archive)

---

## Related Tools

- [list_directory](list_directory.md) - List directory contents
- [search_files](search_files.md) - Find files