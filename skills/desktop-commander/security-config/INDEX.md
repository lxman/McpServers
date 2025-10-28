# Security Configuration

Configure and test Desktop Commander's security policies: directory whitelisting and command blacklisting.

**See [../COMMON.md](../COMMON.md) for shared concepts: security model, path parameters.**

---

## Tools Quick Reference

| Tool | Purpose | Key Parameters |
|------|---------|----------------|
| [get_configuration](get_configuration.md) | Get current security settings | (none) |
| [add_allowed_directory](add_allowed_directory.md) | Add directory to whitelist | directoryPath |
| [add_blocked_command](add_blocked_command.md) | Add command to blacklist | commandPattern |
| [test_directory_access](test_directory_access.md) | Test if directory is allowed | directoryPath |
| [test_command_blocking](test_command_blocking.md) | Test if command is blocked | command |

---

## Security Model Overview

### Two-Layer Security

**1. Directory Whitelisting:**
- Allowed directories list
- Write/delete operations require whitelist
- Read operations work anywhere (OS permissions)
- Subdirectories automatically included

**2. Command Blacklisting:**
- Blocked commands list
- Pattern-based matching
- Prevents dangerous operations
- Supports wildcards

---

## Common Workflows

### Initial Setup
```
1. get_configuration()
   → Review default settings
   → Note any pre-configured paths

2. add_allowed_directory(directoryPath: "C:\\Projects")
   → Grant access to projects folder

3. test_directory_access(directoryPath: "C:\\Projects\\MyApp")
   → Verify access works (subdirectories included)

4. add_blocked_command(commandPattern: "format")
   → Block dangerous commands
```

### Before File Operations
```
1. test_directory_access(directoryPath: "C:\\MyProject")
   → Check if access is allowed

2. If denied:
   add_allowed_directory(directoryPath: "C:\\MyProject")

3. Proceed with file operations
```

### Before Command Execution
```
1. test_command_blocking(command: "npm install")
   → Verify command is allowed

2. If blocked:
   - Review why it's blocked
   - Adjust blocked commands if appropriate
   - Or use alternative approach

3. Execute command
```

### Troubleshooting Access Denied
```
Error: "Path not in allowed directories"

1. test_directory_access(directoryPath: path)
   → Confirm it's denied

2. Check parent directories:
   test_directory_access(directoryPath: parent)

3. Add appropriate directory:
   add_allowed_directory(directoryPath: best_level)

4. Retry operation
```

---

## Configuration Details

### get_configuration Response

```json
{
  "allowedDirectories": [
    "C:\\Projects",
    "C:\\Users\\username\\Documents",
    "C:\\Temp"
  ],
  "blockedCommands": [
    "format",
    "del /s /q C:\\*",
    "rm -rf /",
    "shutdown"
  ],
  "totalAllowedDirectories": 3,
  "totalBlockedCommands": 4,
  "configPath": "C:\\DesktopCommander\\security-config.json",
  "lastModified": "2025-10-21T10:00:00Z"
}
```

### Directory Access Rules

**Allowed directory: C:\\Projects**

✓ Allowed:
- C:\\Projects (exact match)
- C:\\Projects\\MyApp (subdirectory)
- C:\\Projects\\MyApp\\src\\file.txt (nested)

✗ Denied:
- C:\\Project (different path)
- C:\\Other\\file.txt (not under Projects)

**Multiple allowed directories:**
```
Allowed: ["C:\\Projects", "C:\\Documents", "C:\\Temp"]
→ Access to any path under these three roots
```

### Command Blocking Rules

**Pattern matching:**
- Case-insensitive
- Substring matching
- Wildcards supported (`*`)

**Examples:**
```
Blocked: "format"
  ✗ format c:
  ✗ FORMAT D:
  ✗ diskpart format
  ✓ reformat (doesn't match exactly)

Blocked: "del /s"
  ✗ del /s /q C:\*
  ✗ DEL /S C:\temp
  ✓ del file.txt (no /s flag)

Blocked: "rm -rf *"
  ✗ rm -rf /
  ✗ rm -rf /home
  ✓ rm file.txt
```

---

## Best Practices

### Directory Whitelisting

1. **Principle of Least Privilege:**
   ```
   ✓ Specific: C:\Projects\MyApp
   ✗ Too broad: C:\
   ```

2. **Organize by project:**
   ```
   C:\Projects\
     ├── Project1\
     ├── Project2\
     └── Project3\
   
   Add: C:\Projects
   → All projects accessible
   ```

3. **Temporary access:**
   ```
   For one-time task:
     1. add_allowed_directory
     2. Perform task
     3. Manually remove from config later
   ```

4. **Regular review:**
   ```
   Monthly:
     1. get_configuration
     2. Review allowedDirectories
     3. Remove unused paths
   ```

### Command Blocking

1. **Always block destructive commands:**
   ```
   Essential blocks:
     - format
     - del /s /q C:\*
     - rm -rf /
     - shutdown
     - diskpart
   ```

2. **Be specific with patterns:**
   ```
   ✓ Good: "format c:"
   ✗ Too broad: "for" (blocks "foreach")
   ```

3. **Test before blocking:**
   ```
   1. test_command_blocking with pattern
   2. Test multiple variations
   3. Ensure doesn't block legitimate commands
   4. Add if sufficiently specific
   ```

4. **Environment-specific:**
   ```
   Production: More restrictive
   Development: More permissive
   ```

### Testing

1. **Test before operations:**
   ```
   Always test first:
     - test_directory_access before file operations
     - test_command_blocking before execution
   ```

2. **Automated testing:**
   ```
   CI/CD pipeline:
     1. test_directory_access for deploy paths
     2. Fail if access denied
     3. Alert to fix configuration
   ```

---

## Common Use Cases

### Project Onboarding
```
New project: C:\Projects\NewApp

1. add_allowed_directory(directoryPath: "C:\\Projects\\NewApp")
2. test_directory_access to verify
3. Document in project setup guide
```

### Shared Team Configuration
```
Team needs access to:
  - C:\Projects
  - C:\Shared\Resources

1. get_configuration (export current)
2. Add both directories
3. Export configuration
4. Share with team
5. Team imports same config
```

### Security Hardening
```
Production environment:
  1. Review all allowedDirectories
  2. Remove unnecessary paths
  3. Add comprehensive blockedCommands
  4. Test critical operations
  5. Document configuration
```

### Compliance Audit
```
Quarterly review:
  1. get_configuration
  2. Document all allowed directories
  3. Justify each entry
  4. Review blocked commands
  5. Update as needed
```

---

## Configuration File

### Location

```
C:\DesktopCommander\security-config.json
```

### Format

```json
{
  "allowedDirectories": [
    "C:\\Projects",
    "C:\\Users\\username\\Documents"
  ],
  "blockedCommands": [
    "format",
    "del /s /q C:\\*",
    "shutdown"
  ],
  "version": "1.0",
  "lastModified": "2025-10-21T10:00:00Z"
}
```

### Manual Editing

**If you must edit manually:**
1. Stop Desktop Commander
2. Edit security-config.json
3. Validate JSON syntax
4. Restart Desktop Commander
5. Verify with get_configuration

**Recommended:**
- Use tools instead of manual editing
- Changes via tools are validated
- Prevents configuration corruption

---

## Security Considerations

1. **Configuration protection:**
    - Config file should be read-only in production
    - Backup before changes
    - Version control recommended
    - Audit all modifications

2. **Least privilege:**
    - Minimum necessary directories
    - Specific command blocks
    - Regular access reviews
    - Remove unused permissions

3. **Audit trail:**
    - All security changes logged
    - Review with [maintenance/get_audit_log](../maintenance/get_audit_log.md)
    - Track who, what, when
    - Investigate anomalies

4. **Change management:**
    - Document why access granted
    - Review before broad permissions
    - Test blocked commands
    - Require approval for production

---

## Troubleshooting

### Access Denied Unexpectedly
```
Issue: Operation fails with "Path not in allowed directories"
Solutions:
  1. test_directory_access with exact path
  2. Check parent directories
  3. Verify path spelling
  4. add_allowed_directory for correct path
```

### Command Blocked Unexpectedly
```
Issue: Command fails with "Command is blocked"
Solutions:
  1. test_command_blocking with exact command
  2. Review matchedPattern
  3. Determine if block is intentional
  4. If false positive: manually edit config to refine
```

### Configuration Not Saving
```
Issue: Changes don't persist after restart
Solutions:
  - Check config file is writable
  - Verify file path correct
  - Review Desktop Commander logs
  - Check file permissions
```

### Too Many Allowed Directories
```
Issue: Configuration becoming unwieldy
Solutions:
  1. Consolidate under parent directories
  2. Remove unused paths
  3. Document purpose of each
  4. Regular cleanup schedule
```

---

## Advanced Patterns

### Dynamic Access Control
```
Grant temporary access:
  1. add_allowed_directory(tempPath)
  2. Perform time-sensitive operation
  3. Schedule removal after N hours
  4. Automated cleanup
```

### Hierarchical Permissions
```
Different access levels:
  - Admin: C:\ (all access)
  - Developer: C:\Projects
  - User: C:\Users\username\Documents
  
Enforce via separate configurations
```

### Environment-Based Configuration
```
Development config:
  - Broad directory access
  - Fewer blocked commands
  
Production config:
  - Restricted directory access
  - Comprehensive command blocking
  
Load based on environment variable
```

---

**Total Tools:** 5  
**See [../INDEX.md](../INDEX.md) for complete Desktop Commander reference**