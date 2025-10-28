# read_around_line

Read lines around a specific line with context.

**Category:** [file-operations](INDEX.md)  
**Common concepts:** [path parameters](../COMMON.md#path-parameters), [line numbering](../COMMON.md#line-numbering)

---

## Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| filePath | string | ✓ | - | Full path to file |
| lineNumber | integer | ✓ | - | Target line to center on |
| contextLines | integer | ✗ | 10 | Lines before/after to include |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| lines | string[] | Array of line contents |
| targetLine | integer | The target line number |
| startLine | integer | First line returned |
| endLine | integer | Last line returned |
| contextLines | integer | Context lines used |

---

## Examples

**View line 500 with 10 lines of context:**
```
read_around_line(
  filePath: "C:\code\Program.cs",
  lineNumber: 500,
  contextLines: 10
)
→ Returns lines 490-510
```

**Minimal context:**
```
read_around_line(
  filePath: "C:\logs\error.log",
  lineNumber: 1234,
  contextLines: 3
)
→ Returns lines 1231-1237
```

---

## Notes

- **Use case:** Perfect for viewing errors, search results, or specific locations with surrounding code
- **Edge handling:** Adjusts automatically if near start/end of file
- **Context:** contextLines applies both before and after (total = 2×contextLines + 1)
- **Workflow:** Often used after [find_in_file](find_in_file.md) to examine matches

---

## Related Tools

- [read_range](read_range.md) - Read exact range
- [find_in_file](find_in_file.md) - Find line numbers
- [read_file](read_file.md) - General file reading