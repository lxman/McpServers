# find_in_file

Search for text pattern within a file.

**Category:** [file-operations](INDEX.md)

---

## Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| filePath | string | ✓ | - | Full path to file |
| pattern | string | ✓ | - | Search pattern |
| caseSensitive | boolean | ✗ | false | Case-sensitive matching |
| useRegex | boolean | ✗ | false | Use regular expressions |
| maxMatches | integer | ✗ | 500 | Maximum matches to return (1-1000) |
| skip | integer | ✗ | 0 | Number of matches to skip (for pagination) |
| countOnly | boolean | ✗ | false | Return only count, not actual matches |
| includeContext | boolean | ✗ | false | Include surrounding lines for context |
| contextLines | integer | ✗ | 2 | Number of context lines before/after (0-10) |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| filePath | string | File searched |
| pattern | string | Pattern used |
| totalMatches | integer | Total number of matches found |
| returnedCount | integer | Number of matches in this response |
| skip | integer | Number of matches skipped |
| maxMatches | integer | Maximum requested |
| hasMore | boolean | Whether more matches are available |
| nextSkip | integer? | Value to use for next page (if hasMore) |
| matches | object[] | Array of match objects (not present if countOnly) |
| firstMatchLine | integer? | Line number of first match (only if countOnly) |
| lastMatchLine | integer? | Line number of last match (only if countOnly) |

**Match object (without context):**
```json
{
  "lineNumber": 42,
  "content": "  // TODO: Fix this"
}
```

**Match object (with context):**
```json
{
  "lineNumber": 42,
  "content": "  // TODO: Fix this",
  "context": [
    {"lineNumber": 40, "content": "function foo() {", "isMatch": false},
    {"lineNumber": 41, "content": "  var x = 1;", "isMatch": false},
    {"lineNumber": 42, "content": "  // TODO: Fix this", "isMatch": true},
    {"lineNumber": 43, "content": "  return x;", "isMatch": false},
    {"lineNumber": 44, "content": "}", "isMatch": false}
  ]
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
- **Response size protection:** If matches exceed 20,000 token limit, response is blocked with error and suggestions (see [../COMMON.md#response-size-limits](../COMMON.md#response-size-limits))
- **Pagination:** Use `skip` and `maxMatches` to iterate through large match sets
- **Count mode:** Use `countOnly=true` to get match statistics without content
- **Context:** Including context significantly increases response size
- **Use case:** Code search, log analysis, finding specific content
- **Follow-up:** Use [read_around_line](read_around_line.md) to see context

---

## Related Tools

- [read_around_line](read_around_line.md) - View matches with context
- [search_files](search_files.md) - Find files first
- [prepare_replace_in_file](../file-editing/prepare_replace_in_file.md) - Replace matches