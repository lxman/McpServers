# analyze_indentation

Analyze file indentation patterns (tabs vs spaces, indent size).

**Category:** [file-operations](INDEX.md)

---

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| filePath | string | ✓ | Full path to file |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| filePath | string | File analyzed |
| indentType | string | "spaces", "tabs", or "mixed" |
| indentSize | integer | Most common indent size (spaces) |
| tabsCount | integer | Lines using tabs |
| spacesCount | integer | Lines using spaces |
| mixedCount | integer | Lines with mixed indentation |
| recommendation | string | Suggested indentation style |

---

## Example

```
analyze_indentation(filePath: "C:\code\MyClass.cs")
→ {
    "indentType": "spaces",
    "indentSize": 4,
    "spacesCount": 450,
    "tabsCount": 0,
    "recommendation": "Use 4 spaces for indentation"
  }
```

---

## Notes

- **Use case:** Code style analysis, formatting decisions, consistency checks
- **File types:** Works with any text file (code, config, etc.)
- **Mixed indentation:** Indicates inconsistent formatting
- **Follow-up:** Use with [prepare_insert_after_line](../file-editing/prepare_insert_after_line.md) for consistent formatting

---

## Related Tools

- [read_file](read_file.md) - Read file contents
- [prepare_insert_after_line](../file-editing/prepare_insert_after_line.md) - Insert with correct indentation