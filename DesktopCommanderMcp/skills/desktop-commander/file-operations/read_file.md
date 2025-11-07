# read_file

Read file contents with automatic pagination for large files.

**Category:** [file-operations](INDEX.md)  
**Common concepts:** [path parameters](../COMMON.md#path-parameters), [versionToken](../COMMON.md#versiontoken), [pagination](../COMMON.md#pagination)

---

## Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| path | string | ✓ | - | Full path to file |
| startLine | integer | ✗ | null | Starting line number (1-based) |
| maxLines | integer | ✗ | 500 | Max lines to return (1-1000) |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| content | string | File contents |
| totalLines | integer | Total lines in file |
| startLine | integer | First line returned |
| endLine | integer | Last line returned |
| hasMore | boolean | More lines available |
| versionToken | string | SHA256 hash for safe editing |
| path | string | Absolute path |

---

## Examples

**Read first 100 lines:**
```
read_file(path: "C:\config\app.json", maxLines: 100)
```

**Continue from line 101:**
```
read_file(path: "C:\config\app.json", startLine: 101, maxLines: 100)
```

**Read entire file (default):**
```
read_file(path: "C:\code\MyClass.cs")
→ Returns up to 500 lines
```

---

## Notes

- **Encoding:** UTF-8
- **Line numbers:** 1-based (first line = 1)
- **Version token:** Use for [safe editing workflow](INDEX.md#safe-file-editing)
- **Large files:** Check `hasMore` flag and use `startLine` for pagination
- **Alternative:** Use [read_range](read_range.md) for specific line ranges

---

## Related Tools

- [read_range](read_range.md) - Read specific line range
- [read_around_line](read_around_line.md) - Read with context
- [read_next_chunk](read_next_chunk.md) - Incremental reading
- [write_file](write_file.md) - Write content to file