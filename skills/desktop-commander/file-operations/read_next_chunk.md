# read_next_chunk

Read next chunk for incremental file processing.

**Category:** [file-operations](INDEX.md)  
**Common concepts:** [pagination](../COMMON.md#pagination)

---

## Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| filePath | string | ✓ | - | Full path to file |
| startLine | integer | ✓ | - | Starting line (1-based) |
| maxLines | integer | ✗ | 100 | Max lines to return |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| lines | string[] | Array of line contents |
| startLine | integer | First line returned |
| endLine | integer | Last line returned |
| hasMore | boolean | More lines available |
| totalLinesRead | integer | Cumulative lines read |

---

## Example

**Process large file incrementally:**
```
chunk1 = read_next_chunk(filePath: "data.csv", startLine: 1, maxLines: 500)
→ Lines 1-500, hasMore: true

chunk2 = read_next_chunk(filePath: "data.csv", startLine: 501, maxLines: 500)
→ Lines 501-1000, hasMore: true

...continue until hasMore: false
```

---

## Notes

- **Use case:** Processing large files in chunks (logs, CSV, data files)
- **Cleaner pagination:** More semantic than read_file with startLine
- **Memory efficient:** Only loads chunk into memory

---

## Related Tools

- [read_file](read_file.md) - With pagination
- [read_range](read_range.md) - Specific range