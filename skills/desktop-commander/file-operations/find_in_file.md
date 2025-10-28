# find_in_file

Search for text pattern within a file.

**Category:** [file-operations](INDEX.md)

---

## Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| filePath | string | ✓ | - | Full path to file |
| pattern | string | ✓ | - | Search pattern |
| useRegex | boolean | ✗ | false | Use regular expressions |
| caseSensitive | boolean | ✗ | false | Case-sensitive matching |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| matches | object[] | Array of match objects |
| filePath | string | File searched |
| pattern | string | Pattern used |
| matchCount | integer | Number of matches |

**Match object:**
```json
{
  "lineNumber": 42,
  "lineContent": "  // TODO: Fix this",
  "matchPosition": 5
}
```

---

## Examples

**Find TODOs:**
```
find_in_file(
  filePath: "C:\code\MyClass.cs",
  pattern: "TODO"
)
```

**Case-sensitive search:**
```
find_in_file(
  filePath: "config.json",
  pattern: "Error",
  caseSensitive: true
)
```

**Regex search:**
```
find_in_file(
  filePath: "app.log",
  pattern: "ERROR|WARN|FATAL",
  useRegex: true
)
```

---

## Notes

- **Line numbers:** 1-based for use with [read_around_line](read_around_line.md)
- **Use case:** Code search, log analysis, finding specific content
- **Follow-up:** Use [read_around_line](read_around_line.md) to see context

---

## Related Tools

- [read_around_line](read_around_line.md) - View matches with context
- [search_files](search_files.md) - Find files first
- [prepare_replace_in_file](../file-editing/prepare_replace_in_file.md) - Replace matches