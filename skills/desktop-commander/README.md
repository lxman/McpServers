# Desktop Commander Skills - Navigation Guide

## Overview

Desktop Commander provides comprehensive system automation capabilities organized into specialized skill areas. Each area has its own detailed SKILL.md documentation.

**Choose the appropriate skill folder based on your task:**

---

## 📂 Skill Categories

### [file-operations](file-operations/SKILL.md)
**When to use:** File system access, reading, writing, searching, and directory management

**Covers:**
- read_file, write_file, read_range, read_around_line, read_next_chunk
- list_directory, get_file_info, create_directory
- move, delete, search_files, find_in_file
- analyze_indentation

**Use cases:**
- Reading configuration files
- Creating/modifying files
- Searching for files or content
- Directory navigation
- File metadata inspection

---

### [http-operations](http-operations/SKILL.md)
**When to use:** Making HTTP requests, interacting with REST APIs, calling MCP servers

**Covers:**
- http_get, http_post, http_put, http_delete
- http_request (custom methods and headers)

**Use cases:**
- Calling MCP server endpoints
- REST API integration
- Fetching web resources
- Posting data to services
- Custom HTTP operations

---

### [binary-operations](binary-operations/SKILL.md)
**When to use:** Analyzing binary files, hex dumps, pattern searching, file comparison

**Covers:**
- read_hex_bytes, generate_hex_dump
- search_hex_pattern, compare_binary_files

**Use cases:**
- File format analysis
- Binary file inspection
- Finding embedded data
- File integrity verification
- Hex pattern searching

---

### [process-management](process-management/SKILL.md)
**When to use:** Monitoring or controlling running processes

**Covers:**
- list_processes, get_process_info
- kill_process, kill_process_by_name

**Use cases:**
- Finding processes by name
- Monitoring resource usage
- Terminating processes
- Process debugging
- Resource cleanup

---

### [command-execution](command-execution/SKILL.md)
**When to use:** Running shell commands, managing terminal sessions

**Covers:**
- execute_command
- list_sessions, get_session_output
- send_input, close_session

**Use cases:**
- Build automation
- Script execution
- Git operations
- System administration
- Interactive command automation

---

### [file-editing](file-editing/SKILL.md)
**When to use:** Safely modifying file contents with approval workflow

**Covers:**
- prepare_replace_lines, prepare_replace_in_file
- prepare_delete_lines, prepare_insert_after_line
- approve_edit, cancel_edit, list_pending_edits

**Use cases:**
- Safe file modifications
- Code refactoring
- Configuration updates
- Multi-step file edits
- Reviewed changes

**Important:** Uses two-phase workflow (prepare → approve)

---

### [service-management](service-management/SKILL.md)
**When to use:** Managing MCP server services and lifecycle

**Covers:**
- list_servers, start_service, stop_service
- get_service_status, reload_server_registry

**Use cases:**
- Starting/stopping MCP servers
- Service health monitoring
- Configuration reloading
- Service discovery
- Uptime tracking

---

### [security-config](security-config/SKILL.md)
**When to use:** Managing access controls and security policies

**Covers:**
- get_configuration
- add_allowed_directory, add_blocked_command
- test_directory_access, test_command_blocking

**Use cases:**
- Granting directory access
- Blocking dangerous commands
- Access troubleshooting
- Security policy management
- Compliance checking

---

### [maintenance](maintenance/SKILL.md)
**When to use:** Audit logging, cleanup, and system health

**Covers:**
- get_audit_log, cleanup_backup_files
- get_help

**Use cases:**
- Security auditing
- Compliance reporting
- Backup cleanup
- Disk space management
- Troubleshooting
- Activity review

---

## 🎯 Quick Task Routing

**"I need to..."**

- **Read a file** → [file-operations](file-operations/SKILL.md)
- **Call an API** → [http-operations](http-operations/SKILL.md)
- **Analyze a binary file** → [binary-operations](binary-operations/SKILL.md)
- **Find a process** → [process-management](process-management/SKILL.md)
- **Run a command** → [command-execution](command-execution/SKILL.md)
- **Edit a file safely** → [file-editing](file-editing/SKILL.md)
- **Start an MCP server** → [service-management](service-management/SKILL.md)
- **Grant directory access** → [security-config](security-config/SKILL.md)
- **Review audit logs** → [maintenance](maintenance/SKILL.md)

---

## 📋 Common Workflows

### Working with MCP Servers

1. **Discover available servers:**
   - Use [service-management](service-management/SKILL.md) → list_servers

2. **Start a server:**
   - Use [service-management](service-management/SKILL.md) → start_service

3. **Call server endpoints:**
   - Use [http-operations](http-operations/SKILL.md) → http_get/http_post

4. **Monitor server:**
   - Use [service-management](service-management/SKILL.md) → get_service_status

5. **Stop server:**
   - Use [service-management](service-management/SKILL.md) → stop_service

### Safe File Editing

1. **Read file:**
   - Use [file-operations](file-operations/SKILL.md) → read_file

2. **Prepare changes:**
   - Use [file-editing](file-editing/SKILL.md) → prepare_* functions

3. **Review diff:**
   - Examine returned diff

4. **Apply changes:**
   - Use [file-editing](file-editing/SKILL.md) → approve_edit

5. **Verify:**
   - Use [file-operations](file-operations/SKILL.md) → read_file again

### Troubleshooting Access Issues

1. **Check security config:**
   - Use [security-config](security-config/SKILL.md) → get_configuration

2. **Test directory access:**
   - Use [security-config](security-config/SKILL.md) → test_directory_access

3. **Add directory if needed:**
   - Use [security-config](security-config/SKILL.md) → add_allowed_directory

4. **Review audit log:**
   - Use [maintenance](maintenance/SKILL.md) → get_audit_log

---

## 🔑 Key Concepts

### Version Tokens
- Returned by file read operations
- Required for safe file editing
- Detects concurrent modifications
- See [file-editing](file-editing/SKILL.md) for details

### Session Management
- Persistent terminal sessions for commands
- Environment variables preserved
- Working directory maintained
- See [command-execution](command-execution/SKILL.md) for details

### Two-Phase Editing
- Phase 1: Prepare changes (get approval token)
- Phase 2: Approve changes (apply with confirmation)
- Prevents accidental data loss
- See [file-editing](file-editing/SKILL.md) for details

### Security Model
- Allowed directory whitelist
- Blocked command patterns
- Path validation on all operations
- See [security-config](security-config/SKILL.md) for details

---

## 💡 Tips for AI Assistants

1. **Read the appropriate SKILL.md first:**
   - Don't guess at parameters
   - Follow documented patterns
   - Check examples

2. **Use focused documentation:**
   - Only read the skill you need
   - Reduces context size
   - Faster decision making

3. **Follow workflows:**
   - Each SKILL.md has common workflows
   - Use proven patterns
   - Avoid mistakes

4. **Check security first:**
   - Verify directory access before file ops
   - Test command blocking before execution
   - Review audit logs when troubleshooting

5. **Handle errors properly:**
   - Each SKILL.md has troubleshooting section
   - Common errors documented
   - Solutions provided

---

## 📊 Tool Distribution

**Total Tools:** ~45 tools across 9 categories

**Breakdown:**
- File Operations: 13 tools
- HTTP Operations: 5 tools
- Binary Operations: 4 tools
- Process Management: 4 tools
- Command Execution: 5 tools
- File Editing: 6 tools
- Service Management: 5 tools
- Security Config: 5 tools
- Maintenance: 3 tools

---

## 🔄 Updates

When new tools are added:
1. Add to appropriate skill category
2. Update category SKILL.md
3. Update this README
4. Document parameters and examples

---

**Last Updated:** 2025-10-21  
**Token Budget:** ~103,013 tokens remaining