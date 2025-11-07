# search_files

Find files by pattern (with wildcard support).

**Category:** [file-operations](INDEX.md)  
**Common concepts:** [pattern matching](../COMMON.md#pattern-matching)

---

## Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| searchPath | string | ✓ | - | Directory to search in |
| pattern | string | ✓ | - | File pattern (e.g., "*.txt", "test*.cs") |
| recursive | boolean | ✗ | true | Search subdirectories |
| skip | integer | ✗ | 0 | Number of results to skip (for pagination) |
| maxResults | integer | ✗ | 500 | Maximum results to return (1-1000) |
| summaryOnly | boolean | ✗ | false | Return summary with counts instead of full results |
| sortBy | string | ✗ | "name" | Sort by: "name", "size", or "date" |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| totalFound | integer | Total number of matching files |
| returnedCount | integer | Number of files in this response |
| skip | integer | Number of results skipped |
| maxResults | integer | Maximum requested |
| hasMore | boolean | Whether more results are available |
| nextSkip | integer? | Value to use for next page (if hasMore) |
| files | object[] | Array of file objects (not present if summaryOnly) |
| sample | object[]? | Sample files (only if summaryOnly) |
| topDirectories | object[]? | Top directories by file count (only if summaryOnly) |

**File object:**
```json
{
  "path": "C:\\full\\path\\to\\file.txt",
  "name": "file.txt",
  "directory": "C:\\full\\path\\to",
  "size": 1024,
  "modified": "2025-10-21T12:34:56"
}
```

---

## Examples

**Find all C# files:**
```
search_files(
  searchPath: "C:\Projects\MyApp",
  pattern: "*.cs",
  recursive: true
)
```

**Find test files (non-recursive):**
```
search_files(
  searchPath: "C:\Projects\MyApp\tests",
  pattern: "test*.js",
  recursive: false
)
```

**Find specific file:**
```
search_files(
  searchPath: "C:\Projects",
  pattern: "config.json"
)
```

---

## Notes

- **Wildcards:** `*` (any characters), `?` (single character)
- **Case-insensitive:** Pattern matching ignores case
- **Performance:** Large directories may be slow, use specific patterns
- **Response size protection:** If results exceed 20,000 token limit, response is blocked with error and suggestions (see [../COMMON.md#response-size-limits](../COMMON.md#response-size-limits))
- **Pagination:** Use `skip` and `maxResults` to iterate through large result sets
- **Summary mode:** Use `summaryOnly=true` to get overview without full file list
- **Use case:** Code discovery, finding files, bulk operations

---

## Related Tools

- [find_in_file](find_in_file.md) - Search file contents
- [list_directory](list_directory.md) - List specific directory
- [read_file](read_file.md) - Read found files