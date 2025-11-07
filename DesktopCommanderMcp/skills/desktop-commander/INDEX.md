# Desktop Commander - Quick Reference Index

Complete reference for all 50 Desktop Commander tools organized by category.

**See [COMMON.md](COMMON.md) for shared concepts, parameters, and patterns.**

---

## File Operations (13 tools)

| Tool | Purpose | Key Parameters |
|------|---------|----------------|
| [read_file](file-operations/read_file.md) | Read file with pagination | path, startLine?, maxLines? |
| [read_range](file-operations/read_range.md) | Read specific line range | filePath, startLine, endLine |
| [read_around_line](file-operations/read_around_line.md) | Read lines with context | filePath, lineNumber, contextLines? |
| [read_next_chunk](file-operations/read_next_chunk.md) | Incremental file reading | filePath, startLine, maxLines? |
| [write_file](file-operations/write_file.md) | Write or append to file | path, content, mode?, versionToken? |
| [list_directory](file-operations/list_directory.md) | List directory contents | path |
| [get_file_info](file-operations/get_file_info.md) | Get file/directory metadata | path |
| [create_directory](file-operations/create_directory.md) | Create new directory | path |
| [move](file-operations/move.md) | Move or rename file/directory | sourcePath, destinationPath |
| [delete](file-operations/delete.md) | Delete file or directory | path, force? |
| [search_files](file-operations/search_files.md) | Find files by pattern | searchPath, pattern, recursive? |
| [find_in_file](file-operations/find_in_file.md) | Search text within file | filePath, pattern, useRegex?, caseSensitive? |
| [analyze_indentation](file-operations/analyze_indentation.md) | Detect indentation style | filePath |

**[Category Overview](file-operations/INDEX.md)**

---

## HTTP Operations (5 tools)

| Tool | Purpose | Key Parameters |
|------|---------|----------------|
| [http_get](http-operations/http_get.md) | GET request | url |
| [http_post](http-operations/http_post.md) | POST request with JSON body | url, jsonBody |
| [http_put](http-operations/http_put.md) | PUT request with JSON body | url, jsonBody |
| [http_delete](http-operations/http_delete.md) | DELETE request | url |
| [http_request](http-operations/http_request.md) | Custom HTTP request | method, url, headersJson?, jsonBody? |

**[Category Overview](http-operations/INDEX.md)**

---

## Binary Operations (4 tools)

| Tool | Purpose | Key Parameters |
|------|---------|----------------|
| [read_hex_bytes](binary-operations/read_hex_bytes.md) | Read bytes in hex format | filePath, offset, length, format? |
| [generate_hex_dump](binary-operations/generate_hex_dump.md) | Generate classic hex dump | filePath, offset?, length?, bytesPerLine? |
| [search_hex_pattern](binary-operations/search_hex_pattern.md) | Find hex pattern in file | filePath, hexPattern, startOffset?, maxResults? |
| [compare_binary_files](binary-operations/compare_binary_files.md) | Compare two binary files | file1Path, file2Path, offset?, length?, showMatches? |

**[Category Overview](binary-operations/INDEX.md)**

---

## Process Management (4 tools)

| Tool | Purpose | Key Parameters |
|------|---------|----------------|
| [list_processes](process-management/list_processes.md) | List running processes | filter?, limit? |
| [get_process_info](process-management/get_process_info.md) | Get detailed process info | processId |
| [kill_process](process-management/kill_process.md) | Terminate process by ID | processId, force? |
| [kill_process_by_name](process-management/kill_process_by_name.md) | Terminate all processes by name | processName, confirmation? |

**[Category Overview](process-management/INDEX.md)**

---

## Command Execution (5 tools)

| Tool | Purpose | Key Parameters |
|------|---------|----------------|
| [execute_command](command-execution/execute_command.md) | Execute shell command | command, sessionId?, workingDirectory?, timeoutMs? |
| [send_input](command-execution/send_input.md) | Send input to session | sessionId, input |
| [get_session_output](command-execution/get_session_output.md) | Get session output buffer | sessionId? |
| [list_sessions](command-execution/list_sessions.md) | List active sessions | (none) |
| [close_session](command-execution/close_session.md) | Close terminal session | sessionId |

**[Category Overview](command-execution/INDEX.md)**

---

## File Editing (6 tools)

**Two-phase editing:** All edits require prepare + approve for safety.

| Tool | Purpose | Key Parameters |
|------|---------|----------------|
| [prepare_replace_lines](file-editing/prepare_replace_lines.md) | Prepare line range replacement | filePath, startLine, endLine, newContent, versionToken |
| [prepare_replace_in_file](file-editing/prepare_replace_in_file.md) | Prepare text pattern replacement | filePath, searchPattern, replaceWith, versionToken, useRegex? |
| [prepare_delete_lines](file-editing/prepare_delete_lines.md) | Prepare line range deletion | filePath, startLine, endLine, versionToken |
| [prepare_insert_after_line](file-editing/prepare_insert_after_line.md) | Prepare content insertion | filePath, afterLine, content, versionToken |
| [approve_edit](file-editing/approve_edit.md) | Apply pending edit | approvalToken, confirmation |
| [cancel_edit](file-editing/cancel_edit.md) | Cancel pending edit | approvalToken |
| [list_pending_edits](file-editing/list_pending_edits.md) | List pending edits | (none) |

**[Category Overview](file-editing/INDEX.md)**

---

## Service Management (5 tools)

| Tool | Purpose | Key Parameters |
|------|---------|----------------|
| [list_servers](service-management/list_servers.md) | Get MCP server directory | (none) |
| [get_service_status](service-management/get_service_status.md) | Get service status and metrics | serviceId |
| [start_service](service-management/start_service.md) | Start MCP service | serviceName, workingDirectory?, forceInit? |
| [stop_service](service-management/stop_service.md) | Stop MCP service | serviceId |
| [reload_server_registry](service-management/reload_server_registry.md) | Reload service configuration | (none) |

**[Category Overview](service-management/INDEX.md)**

---

## Security Configuration (5 tools)

| Tool | Purpose | Key Parameters |
|------|---------|----------------|
| [get_configuration](security-config/get_configuration.md) | Get current security settings | (none) |
| [add_allowed_directory](security-config/add_allowed_directory.md) | Add directory to whitelist | directoryPath |
| [add_blocked_command](security-config/add_blocked_command.md) | Add command to blacklist | commandPattern |
| [test_directory_access](security-config/test_directory_access.md) | Test if directory is allowed | directoryPath |
| [test_command_blocking](security-config/test_command_blocking.md) | Test if command is blocked | command |

**[Category Overview](security-config/INDEX.md)**

---

## Maintenance (3 tools)

| Tool | Purpose | Key Parameters |
|------|---------|----------------|
| [get_audit_log](maintenance/get_audit_log.md) | Get recent audit entries | count? |
| [cleanup_backup_files](maintenance/cleanup_backup_files.md) | Clean up old backup files | directoryPath, olderThanHours?, pattern? |
| [get_help](maintenance/get_help.md) | Get help information | (none) |

**[Category Overview](maintenance/INDEX.md)**

---

## Common Workflows

### Safe File Editing
```
1. read_file → get content + versionToken
2. prepare_replace_lines → get approvalToken + preview
3. approve_edit → apply changes
```

### Security Setup
```
1. get_configuration → review settings
2. test_directory_access → check access
3. add_allowed_directory → grant access if needed
```

### Process Management
```
1. list_processes → find target process
2. get_process_info → verify details
3. kill_process → terminate
```

### Command Execution
```
1. execute_command → run command in session
2. get_session_output → retrieve results
3. close_session → cleanup
```

### File Discovery
```
1. search_files → find files by pattern
2. find_in_file → search content
3. read_file → examine matches
```

---

## Quick Navigation

**By Use Case:**

- **Need to read files?** → [file-operations](file-operations/INDEX.md)
- **Need to edit safely?** → [file-editing](file-editing/INDEX.md)
- **Need to run commands?** → [command-execution](command-execution/INDEX.md)
- **Need to manage processes?** → [process-management](process-management/INDEX.md)
- **Need to call APIs?** → [http-operations](http-operations/INDEX.md)
- **Need to work with binary data?** → [binary-operations](binary-operations/INDEX.md)
- **Need to manage services?** → [service-management](service-management/INDEX.md)
- **Need to configure security?** → [security-config](security-config/INDEX.md)
- **Need to audit or cleanup?** → [maintenance](maintenance/INDEX.md)

**By Frequency:**

Most commonly used tools:
1. [read_file](file-operations/read_file.md) - Read file contents
2. [write_file](file-operations/write_file.md) - Write to file
3. [execute_command](command-execution/execute_command.md) - Run commands
4. [list_directory](file-operations/list_directory.md) - List files
5. [prepare_replace_lines](file-editing/prepare_replace_lines.md) - Edit files safely

---

## Getting Started

### First Time Setup

1. **Check Security Configuration**
   ```
   get_configuration()
   ```

2. **Add Your Project Directory**
   ```
   add_allowed_directory(directoryPath: "C:\\MyProjects")
   ```

3. **Verify Access**
   ```
   test_directory_access(directoryPath: "C:\\MyProjects\\MyApp")
   ```

4. **Start Using Tools**
   ```
   read_file(path: "C:\\MyProjects\\MyApp\\README.md")
   ```

### Learning Path

**Beginner:**
- Start with [file-operations](file-operations/INDEX.md)
- Learn [security-config](security-config/INDEX.md)
- Try [command-execution](command-execution/INDEX.md)

**Intermediate:**
- Master [file-editing](file-editing/INDEX.md) two-phase workflow
- Explore [process-management](process-management/INDEX.md)
- Use [maintenance](maintenance/INDEX.md) for auditing

**Advanced:**
- Leverage [binary-operations](binary-operations/INDEX.md)
- Integrate [http-operations](http-operations/INDEX.md)
- Manage [service-management](service-management/INDEX.md)

---

## Documentation Structure

```
desktop-commander/
├── README.md           - Overview and introduction
├── COMMON.md          - Shared concepts (READ THIS FIRST)
├── INDEX.md           - This file (quick reference)
│
├── file-operations/
│   ├── INDEX.md       - Category overview
│   └── {tool}.md      - Individual tool docs
│
├── http-operations/
│   ├── INDEX.md
│   └── {tool}.md
│
├── [... other categories ...]
│
└── maintenance/
    ├── INDEX.md
    └── {tool}.md
```

**Navigation:**
1. Start with [COMMON.md](COMMON.md) for shared concepts
2. Use this INDEX.md for quick tool lookup
3. Visit category INDEX.md for category overview
4. Read individual tool.md for detailed documentation

---

**Total Tools:** 50  
**Total Categories:** 9  
**Documentation Files:** 60+ (including category and tool files)

**See [COMMON.md](COMMON.md) for parameters, response formats, and patterns shared across all tools.**