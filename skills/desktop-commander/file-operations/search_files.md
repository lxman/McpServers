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

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| files | string[] | Array of matching file paths |
| searchPath | string | Directory searched |
| pattern | string | Pattern used |
| fileCount | integer | Number of matches |

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
- **Use case:** Code discovery, finding files, bulk operations

---

## Related Tools

- [find_in_file](find_in_file.md) - Search file contents
- [list_directory](list_directory.md) - List specific directory
- [read_file](read_file.md) - Read found files