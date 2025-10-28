# File Operations

Comprehensive file system operations with security controls. All operations respect the allowed directory whitelist.

**See [../COMMON.md](../COMMON.md) for shared concepts: path parameters, version tokens, security model.**

---

## Tools Quick Reference

| Tool | Purpose | Key Parameters |
|------|---------|----------------|
| [read_file](read_file.md) | Read file with pagination | path, startLine?, maxLines? |
| [read_range](read_range.md) | Read specific line range | filePath, startLine, endLine |
| [read_around_line](read_around_line.md) | Read lines with context | filePath, lineNumber, contextLines? |
| [read_next_chunk](read_next_chunk.md) | Incremental file reading | filePath, startLine, maxLines? |
| [write_file](write_file.md) | Write or append to file | path, content, mode?, versionToken? |
| [list_directory](list_directory.md) | List directory contents | path |
| [get_file_info](get_file_info.md) | Get file/directory metadata | path |
| [create_directory](create_directory.md) | Create new directory | path |
| [move](move.md) | Move or rename file/directory | sourcePath, destinationPath |
| [delete](delete.md) | Delete file or directory | path, force? |
| [search_files](search_files.md) | Find files by pattern | searchPath, pattern, recursive? |
| [find_in_file](find_in_file.md) | Search text within file | filePath, pattern, useRegex?, caseSensitive? |
| [analyze_indentation](analyze_indentation.md) | Detect indentation style | filePath |

---

## Common Workflows

### Reading Large Files
```
1. read_file(path, maxLines=500)
   → Get first 500 lines + versionToken + hasMore flag

2. If hasMore:
   read_file(path, startLine=501, maxLines=500)
   → Get next 500 lines

3. Or use read_next_chunk for cleaner pagination
```

### Safe File Editing
```
1. read_file(path)
   → Get content + versionToken: "sha256:abc123"

2. Modify content locally

3. write_file(path, content, versionToken: "sha256:abc123")
   → Prevents overwrites if file changed
   → Returns error if token mismatch
```

### File Discovery
```
1. search_files(searchPath: "C:\Projects", pattern: "*.cs", recursive: true)
   → Find all C# files

2. For each file:
   find_in_file(filePath, pattern: "TODO")
   → Search for TODOs

3. read_around_line(filePath, lineNumber, contextLines: 5)
   → View TODO with context
```

### Directory Operations
```
1. list_directory(path: "C:\Projects")
   → Get all items with metadata

2. get_file_info(path: "C:\Projects\MyApp")
   → Detailed info: size, dates, attributes

3. create_directory(path: "C:\Projects\NewApp")
   → Create new project directory
```

---

## Security Considerations

### Required Permissions

**Read Operations:**
- Work on any OS-accessible path
- No allowed directory check required
- Subject to OS file permissions only

**Write/Delete Operations:**
- **Require** path within allowed directories
- Check security-config for allowed paths
- Use [test_directory_access](../security-config/test_directory_access.md) to verify

### Example: Checking Access
```
1. test_directory_access(directoryPath: "C:\MyProject")
   → isAllowed: false

2. add_allowed_directory(directoryPath: "C:\MyProject")
   → success: true

3. test_directory_access(directoryPath: "C:\MyProject")
   → isAllowed: true

4. write_file(path: "C:\MyProject\file.txt", content: "...")
   → Now succeeds
```

---

## Best Practices

### File Reading

1. **Use appropriate tool for use case:**
    - Full file access: `read_file`
    - Specific lines: `read_range`
    - Context around line: `read_around_line`
    - Large file processing: `read_next_chunk`

2. **Pagination for large files:**
    - Set reasonable `maxLines` (default: 500)
    - Check `hasMore` flag
    - Use `startLine` for next chunk

3. **Always capture versionToken:**
    - Needed for safe editing
    - Prevents race conditions
    - Validates file hasn't changed

### File Writing

1. **Use versionToken for safety:**
    - Read file first to get token
    - Modify content
    - Write with token
    - Handle mismatch gracefully

2. **Choose appropriate mode:**
    - `overwrite` (default): Replace entire file
    - `append`: Add to end of file

3. **Create backups for critical files:**
    - Use file-editing tools for automatic backups
    - Or manually copy before write

### Directory Operations

1. **Check before bulk operations:**
    - Test access permissions first
    - Verify paths are correct
    - Handle errors gracefully

2. **Use recursive search carefully:**
    - Can be slow on large directories
    - Consider filtering patterns
    - Set reasonable depth limits

### Pattern Matching

1. **Be specific with patterns:**
    - ✓ Good: `"*.cs"`, `"test*.txt"`
    - ✗ Too broad: `"*.*"`, `"*"`

2. **Use regex when needed:**
    - Simple patterns: wildcard matching
    - Complex patterns: regex with `useRegex: true`

---

## Common Use Cases

### Code Analysis
```
1. search_files(searchPath: "C:\code", pattern: "*.cs")
2. For each file:
   - find_in_file(pattern: "TODO|FIXME|HACK")
   - read_around_line for context
```

### Log Processing
```
1. read_file(path: "app.log", maxLines: 1000)
2. find_in_file(pattern: "ERROR", caseSensitive: false)
3. read_around_line for each error with context
```

### File Organization
```
1. list_directory(path: "C:\Downloads")
2. Filter by extension or date
3. move to organized folders
4. Or delete old files
```

### Project Setup
```
1. create_directory(path: "C:\Projects\NewApp")
2. create_directory(path: "C:\Projects\NewApp\src")
3. create_directory(path: "C:\Projects\NewApp\tests")
4. write_file for initial files
```

---

## Performance Tips

1. **Reading:**
    - Use `maxLines` to limit data transfer
    - Use `read_range` for specific sections
    - Avoid reading entire large files unnecessarily

2. **Searching:**
    - Use specific patterns to reduce matches
    - Set `recursive: false` when possible
    - Consider directory depth

3. **Writing:**
    - Batch writes when possible
    - Use `append` mode for logs
    - Consider file locking for concurrent access

---

## Error Handling

### Common Errors

**File Not Found:**
```json
{
  "success": false,
  "error": "File not found",
  "path": "C:\\nonexistent\\file.txt"
}
```

**Access Denied:**
```json
{
  "success": false,
  "error": "Path not in allowed directories",
  "attemptedPath": "C:\\restricted\\file.txt"
}
```

**Version Token Mismatch:**
```json
{
  "success": false,
  "error": "Version token mismatch - file was modified",
  "expectedToken": "sha256:abc123",
  "currentToken": "sha256:def456"
}
```

### Recovery Strategies

1. **Access denied:** Add directory to allowed list
2. **Version mismatch:** Re-read file and retry
3. **File not found:** Verify path and create if needed
4. **File in use:** Wait and retry, or handle gracefully

---

**Total Tools:** 13  
**See [../INDEX.md](../INDEX.md) for complete Desktop Commander reference**