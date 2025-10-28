# Common Elements - Desktop Commander

This document defines shared concepts, patterns, and parameters used across all Desktop Commander tools.

---

## Standard Parameters

### Path Parameters

All path-related parameters (`path`, `filePath`, `directoryPath`, `sourcePath`, `destinationPath`) follow these rules:

- **Format:** Absolute Windows paths (e.g., `C:\Users\username\Documents\file.txt`)
- **Normalization:** Automatically converted to full absolute paths internally
- **Case Sensitivity:** Windows file system rules apply (case-insensitive)
- **Path Separators:** Both `\` and `/` accepted, normalized to `\`

**Example:**
```
Input:  "C:\Projects\..\MyApp\file.txt"
Output: "C:\MyApp\file.txt"
```

### sessionId

Used in command execution and service management tools.

- **Type:** string
- **Default:** "default"
- **Purpose:** Identifies a persistent terminal/command session
- **Scope:** Session-specific state (working directory, environment variables)
- **Lifecycle:** Exists until explicitly closed or server restart

**Example:**
```
Session "build-process":
  - Working directory: C:\Projects\MyApp
  - Environment: Custom PATH variables
  - Multiple commands share state
```

### versionToken

Used for optimistic concurrency control in file operations.

- **Type:** string
- **Format:** SHA256 hash (e.g., `"sha256:abc123..."`)
- **Purpose:** Prevent accidental overwrites when file changes between read and write
- **Generation:** Automatically computed from file contents on read operations
- **Validation:** Write operations verify token matches current file state

**Workflow:**
```
1. read_file returns versionToken: "sha256:abc123"
2. User modifies content
3. write_file with versionToken: "sha256:abc123"
4. If file unchanged: write succeeds
5. If file changed: write fails (token mismatch)
```

### approvalToken

Used in two-phase file editing operations.

- **Type:** string
- **Format:** "edit_" + unique identifier
- **Purpose:** Link prepare phase to approval phase
- **Lifecycle:** Valid until approved, cancelled, or timeout
- **Scope:** Specific to one pending edit

**Workflow:**
```
1. prepare_* operation returns approvalToken: "edit_456"
2. Review proposed changes
3. approve_edit with approvalToken: "edit_456" + confirmation: "APPROVE"
4. Edit applied and approvalToken invalidated
```

---

## Standard Response Format

All tools return JSON with these minimum fields:

```json
{
  "success": boolean,
  "error": string     // Only present if success: false
  // ...tool-specific fields
}
```

**Success Response Example:**
```json
{
  "success": true,
  "path": "C:\\code\\MyClass.cs",
  "totalLines": 500,
  "versionToken": "sha256:abc123..."
}
```

**Error Response Example:**
```json
{
  "success": false,
  "error": "Path not in allowed directories",
  "attemptedPath": "C:\\restricted\\file.txt"
}
```

---

## Security Model

Desktop Commander implements a whitelist-based security model.

### Directory Access Control

**Allowed Directories:**
- Configured in `security-config.json`
- Whitelist of accessible directory paths
- Subdirectories automatically included
- Required for write/delete operations

**Example:**
```
Allowed: C:\Projects

✓ Can access: C:\Projects\MyApp\src\file.txt
✓ Can access: C:\Projects\OtherApp\data.json
✗ Cannot write to: C:\Windows\System32\file.dll
```

**Enforcement:**
- **Read operations:** Work on any accessible path (OS permissions)
- **Write operations:** Require path within allowed directories
- **Delete operations:** Require path within allowed directories

### Command Blocking

**Blocked Commands:**
- Configured in `security-config.json`
- Blacklist of prohibited command patterns
- Supports wildcards (`*` matches any characters)
- Case-insensitive matching

**Example:**
```
Blocked: "format"

✗ Blocked: "format c:"
✗ Blocked: "FORMAT D:"
✓ Allowed: "npm install"
```

### Configuration

See [security-config/INDEX.md](security-config/INDEX.md) for:
- Viewing current configuration
- Adding allowed directories
- Adding blocked commands
- Testing access permissions

---

## Common Patterns

### Pattern Matching (for search operations)

**Wildcard Support:**
- `*` - Matches any characters (zero or more)
- `?` - Matches single character
- Case-insensitive by default (configurable)

**Examples:**
```
Pattern: "*.txt"
  ✓ Matches: file.txt, document.txt, README.txt
  ✗ Skips: file.doc, image.png

Pattern: "test*.cs"
  ✓ Matches: test1.cs, testFile.cs, test.cs
  ✗ Skips: file.cs, mytest.cs

Pattern: "file?.txt"
  ✓ Matches: file1.txt, fileA.txt
  ✗ Skips: file.txt, file12.txt
```

### Backup File Naming

Desktop Commander creates backups using this format:

```
Original: C:\code\MyClass.cs
Backup:   C:\code\MyClass.cs.backup.20251021143000
```

**Format:** `{original}.backup.{YYYYMMDDHHmmss}`

**Timestamp Breakdown:**
```
20251021143000
  2025 - Year
  10   - Month (October)
  21   - Day
  14   - Hour (24-hour format)
  30   - Minute
  00   - Second
```

### Line Numbering

All line numbers in Desktop Commander are **1-based** (first line = 1).

**Examples:**
```
read_range(startLine=1, endLine=10)    // First 10 lines
read_around_line(lineNumber=50)        // Lines around line 50
prepare_replace_lines(startLine=100)   // Starting at line 100
```

### Pagination

Large results are paginated automatically:

- **Default:** 500 items/lines per page
- **Configurable:** Most tools have `maxLines` or `limit` parameters
- **Navigation:** Use `startLine` or offset parameters for subsequent pages

**Example:**
```
File with 1500 lines:
  
Page 1: read_file(path, maxLines=500)
  → Returns lines 1-500, hasMore: true

Page 2: read_file(path, startLine=501, maxLines=500)
  → Returns lines 501-1000, hasMore: true

Page 3: read_file(path, startLine=1001, maxLines=500)
  → Returns lines 1001-1500, hasMore: false
```

---

## Error Handling

### Common Error Types

**Access Denied:**
```json
{
  "success": false,
  "error": "Path not in allowed directories",
  "attemptedPath": "C:\\restricted\\file.txt",
  "configuredAllowedPaths": ["C:\\Projects", "C:\\Temp"]
}
```

**File Not Found:**
```json
{
  "success": false,
  "error": "File not found",
  "path": "C:\\nonexistent\\file.txt"
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

**Command Blocked:**
```json
{
  "success": false,
  "error": "Command is blocked by security policy",
  "command": "format c:",
  "matchedPattern": "format"
}
```

### Best Practices

1. **Always check success field:**
   ```
   response = call_tool(...)
   if response.success:
       process_result(response)
   else:
       handle_error(response.error)
   ```

2. **Use version tokens for safe editing:**
   ```
   1. Read file → get versionToken
   2. Modify content
   3. Write with versionToken
   4. Handle version mismatch gracefully
   ```

3. **Test security permissions:**
   ```
   Before bulk operations:
     1. test_directory_access
     2. If denied, request access
     3. Proceed with operations
   ```

---

## Tool Categories

Desktop Commander organizes tools into these categories:

- **[file-operations](file-operations/INDEX.md)** - File system operations (13 tools)
- **[http-operations](http-operations/INDEX.md)** - HTTP client (5 tools)
- **[binary-operations](binary-operations/INDEX.md)** - Binary file manipulation (4 tools)
- **[process-management](process-management/INDEX.md)** - Process control (4 tools)
- **[command-execution](command-execution/INDEX.md)** - Shell command execution (5 tools)
- **[file-editing](file-editing/INDEX.md)** - Two-phase file editing (6 tools)
- **[service-management](service-management/INDEX.md)** - MCP service management (5 tools)
- **[security-config](security-config/INDEX.md)** - Security configuration (5 tools)
- **[maintenance](maintenance/INDEX.md)** - Audit and cleanup (3 tools)

**Total:** 50 tools across 9 categories

---

## Quick Start

### 1. Check Security Configuration
```
get_configuration()
→ Review allowed directories and blocked commands
```

### 2. Add Access If Needed
```
test_directory_access(directoryPath: "C:\\MyProject")
→ If denied: add_allowed_directory(directoryPath: "C:\\MyProject")
```

### 3. Use Tools
```
read_file(path: "C:\\MyProject\\file.txt")
→ Returns content and versionToken
```

### 4. Monitor Activity
```
get_audit_log(count: 50)
→ Review recent operations for troubleshooting
```

---

**Reference:** See [INDEX.md](INDEX.md) for complete tool listing with quick descriptions.