# read_range

Read specific line range from a file.

**Category:** [file-operations](INDEX.md)  
**Common concepts:** [path parameters](../COMMON.md#path-parameters), [line numbering](../COMMON.md#line-numbering)

---

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| filePath | string | ✓ | Full path to file |
| startLine | integer | ✓ | Starting line (1-based, inclusive) |
| endLine | integer | ✓ | Ending line (1-based, inclusive) |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| lines | string[] | Array of line contents |
| startLine | integer | First line returned |
| endLine | integer | Last line returned |
| totalLines | integer | Total lines in file |

---

## Examples

**Read lines 50-75:**
```
read_range(
  filePath: "C:\logs\app.log",
  startLine: 50,
  endLine: 75
)
→ Returns 26 lines
```

**Read single line:**
```
read_range(
  filePath: "C:\config.json",
  startLine: 10,
  endLine: 10
)
```

---

## Notes

- **Line numbering:** 1-based, both start and end inclusive
- **Validation:** startLine must be ≤ endLine
- **Use case:** When you know exact line numbers needed
- **Alternative:** Use [read_file](read_file.md) with startLine/maxLines for pagination

---

## Related Tools

- [read_file](read_file.md) - Read with pagination
- [read_around_line](read_around_line.md) - Read with context
- [find_in_file](find_in_file.md) - Find line numbers first