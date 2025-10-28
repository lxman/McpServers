# Maintenance

System health monitoring, audit logging, and cleanup utilities for troubleshooting and compliance.

**See [../COMMON.md](../COMMON.md) for shared concepts: standard responses, backup file format.**

---

## Tools Quick Reference

| Tool | Purpose | Key Parameters |
|------|---------|----------------|
| [get_audit_log](get_audit_log.md) | Get recent audit entries | count? |
| [cleanup_backup_files](cleanup_backup_files.md) | Clean up old backup files | directoryPath, olderThanHours?, pattern? |
| [get_help](get_help.md) | Get help information | (none) |

---

## Common Workflows

### Security Audit
```
1. get_audit_log(count: 100)
   → Retrieve recent operations

2. Filter for security-relevant:
   - File deletions
   - Security config changes
   - Process kills
   - Blocked command attempts

3. Analyze patterns:
   - Unusual access times?
   - Failed operations?
   - Suspicious activity?

4. Generate report
```

### Compliance Reporting
```
1. get_audit_log(count: 1000)
   → Collect audit data

2. Group by operation type:
   - File operations
   - Security changes
   - Service management

3. Verify compliance:
   - All edits approved?
   - Security changes authorized?
   - Dangerous commands blocked?

4. Document findings
```

### Disk Space Management
```
1. List directories with backups
2. Check disk space available
3. cleanup_backup_files(
     directoryPath: "C:\\Projects",
     olderThanHours: 168,  // 7 days
     pattern: "*.backup.*"
   )
4. Verify space freed
5. Schedule regular cleanups
```

### Troubleshooting Issues
```
1. Reproduce failing operation
2. get_audit_log(count: 50)
3. Find related entries:
   - Same path/resource
   - Same time period
   - Success/failure pattern
4. Analyze error details
5. Identify root cause
6. Apply fix
```

---

## Audit Log

### Entry Structure

```json
{
  "timestamp": "2025-10-21T10:30:00Z",
  "operation": "read_file",
  "path": "C:\\code\\MyClass.cs",
  "user": "username",
  "success": true,
  "details": {
    "linesRead": 500,
    "versionToken": "sha256:abc123..."
  }
}
```

### Logged Operations

**File Operations:**
- read_file, write_file, read_range
- create_directory, move, delete
- search_files, find_in_file

**File Editing:**
- prepare_replace_lines, prepare_replace_in_file
- prepare_delete_lines, prepare_insert_after_line
- approve_edit, cancel_edit

**Process Management:**
- kill_process, kill_process_by_name

**Command Execution:**
- execute_command

**Security:**
- add_allowed_directory, add_blocked_command

**Service Management:**
- start_service, stop_service

### Details by Operation

**File Read:**
```json
{
  "linesRead": 500,
  "startLine": 1,
  "endLine": 500,
  "versionToken": "sha256:abc123..."
}
```

**File Edit:**
```json
{
  "startLine": 10,
  "endLine": 15,
  "linesAdded": 4,
  "linesRemoved": 6,
  "approvalToken": "edit_456...",
  "backupCreated": "file.backup.20251021120000"
}
```

**Command Execution:**
```json
{
  "command": "npm install",
  "sessionId": "default",
  "exitCode": 0,
  "executionTime": 1500
}
```

**Security Change:**
```json
{
  "directoryPath": "C:\\Projects\\NewApp",
  "added": true
}
```

---

## Audit Log Analysis

### Success Rate Analysis
```
Total: 100 operations
Successful: 95
Failed: 5
Success rate: 95%

→ Investigate failures
→ Identify common causes
→ Apply fixes
```

### Operation Frequency
```
By type:
  - read_file: 50
  - write_file: 20
  - execute_command: 15
  - prepare_edit: 10
  - approve_edit: 5

→ Identify usage patterns
→ Optimize common operations
```

### Error Clustering
```
By error type:
  - "Path not allowed": 3
  - "Command blocked": 1
  - "Version token mismatch": 1

→ Focus on most common
→ Fix configuration or process
```

### Time-Based Patterns
```
By hour:
  - 09:00-12:00: 40 operations
  - 12:00-15:00: 35 operations
  - 15:00-18:00: 25 operations

→ Peak usage times
→ Off-hours activity (unusual?)
→ Automated task timing
```

### User Activity
```
By user:
  - user1: 75 operations
  - user2: 20 operations
  - user3: 5 operations

→ Track individual usage
→ Identify unusual activity
→ Authorization audit
```

---

## Backup File Management

### Backup File Format

**Standard format:**
```
Original: C:\code\MyClass.cs
Backup:   C:\code\MyClass.cs.backup.20251021143000
```

**Components:**
- Original filename
- `.backup.` marker
- Timestamp: YYYYMMDDHHmmss

### Cleanup Strategy

**By age:**
```
cleanup_backup_files(
  directoryPath: "C:\\Projects",
  olderThanHours: 168  // 7 days
)
```

**By pattern:**
```
cleanup_backup_files(
  directoryPath: "C:\\Projects",
  pattern: "*.cs.backup.*"  // Only C# backups
)
```

**Immediate cleanup (all backups):**
```
cleanup_backup_files(
  directoryPath: "C:\\Temp",
  olderThanHours: 0
)
```

### Recommended Retention

- **Critical files:** 30+ days
- **Project files:** 7 days
- **Temporary files:** 1 day
- **Test files:** 0 days (don't create backups)

---

## Best Practices

### Audit Logging

1. **Regular review:**
    - **Daily:** Last 100 entries, review failures
    - **Weekly:** Full audit review, trend analysis
    - **Monthly:** Comprehensive audit, compliance check

2. **Retention policy:**
    - Retain for compliance period (30-90 days)
    - Back up audit logs regularly
    - Protect from modification
    - Keep accessible for investigation

3. **Alert on anomalies:**
    - High failure rates
    - Unusual access times
    - Repeated blocked commands
    - Suspicious patterns

### Backup Management

1. **Strategic backup creation:**
    - **Always backup:** Production files, critical configs
    - **Optional:** Test files, generated files
    - **Never backup:** Easily reproduced content

2. **Cleanup automation:**
   ```
   Schedule:
     - Daily: Temp directories (1 day retention)
     - Weekly: Project directories (7 days)
     - Monthly: Archive directories (30 days)
   ```

3. **Space monitoring:**
   ```
   Before cleanup:
     - Check disk space
     - Estimate space to free
     - Set appropriate retention
   
   After cleanup:
     - Verify space freed
     - Confirm critical backups kept
     - Document results
   ```

---

## Common Use Cases

### Security Investigation
```
Incident: Unauthorized file deletion

1. get_audit_log(count: 500)
2. Filter for delete operations
3. Identify user and timestamp
4. Review related operations
5. Check security config changes
6. Generate incident report
```

### Performance Analysis
```
Goal: Identify slow operations

1. get_audit_log(count: 200)
2. Extract executionTime from details
3. Sort by duration
4. Identify bottlenecks
5. Optimize slow operations
```

### Disk Space Recovery
```
Low disk space alert:

1. Search for backup accumulation
2. cleanup_backup_files across directories
3. Monitor space freed (mbFreed)
4. Schedule regular cleanups
5. Adjust retention policies
```

### Compliance Documentation
```
Quarterly audit:

1. get_audit_log(count: 5000)
2. Export to report format
3. Categorize operations
4. Document compliance status
5. Archive for retention period
```

---

## Performance Tips

### Audit Log Queries

1. **Use appropriate count:**
    - Don't retrieve more than needed
    - Default (20) sufficient for quick checks
    - Use higher counts for analysis

2. **Filter early:**
    - Retrieve specific range
    - Process on client side if needed
    - Don't over-query

### Backup Cleanup

1. **Specific patterns:**
   ```
   ✓ Good: "*.backup.*", "*.cs.backup.*"
   ✗ Too broad: "*.*"
   ```

2. **Reasonable retention:**
    - Balance safety vs. space
    - Adjust per directory/file type
    - Monitor trends

3. **Scheduled cleanups:**
    - Run during off-hours
    - Avoid peak usage times
    - Monitor impact

---

## Troubleshooting

### Can't Find Recent Operation
```
Issue: Operation not in audit log
Solutions:
  - Increase count parameter
  - Check timestamp range
  - Verify operation actually occurred
  - Check for log rotation
```

### Cleanup Deletes Nothing
```
Issue: cleanup_backup_files finds no files
Solutions:
  - Verify pattern matches actual files
  - Check olderThanHours threshold
  - Confirm files exist in directory
  - Test with olderThanHours: 0
```

### Cleanup Fails with Errors
```
Issue: Some backups not deleted
Solutions:
  - Review errors array in response
  - Check files not in use
  - Verify file permissions
  - Ensure disk not full
  - Close applications using files
```

### Too Many Audit Entries
```
Issue: Difficult to find relevant entries
Solutions:
  - Use specific count range
  - Filter by operation type
  - Filter by path pattern
  - Process and summarize
```

---

## Security Considerations

1. **Audit log integrity:**
    - Logs should be append-only
    - Protected from modification
    - Backed up regularly
    - Retained per policy

2. **Backup file security:**
    - May contain sensitive data
    - Same access controls as originals
    - Clean up regularly
    - Secure deletion if needed

3. **Cleanup operations:**
    - Verify pattern before cleanup
    - Test on non-critical data first
    - Monitor results
    - Preserve critical backups

---

## Help System

### get_help Tool

Returns:
- Desktop Commander version
- Tool categories
- Available tools per category
- Brief descriptions
- Endpoint information

**Use for:**
- Discovering capabilities
- Finding documentation
- Quick reference
- API exploration

---

**Total Tools:** 3  
**See [../INDEX.md](../INDEX.md) for complete Desktop Commander reference**