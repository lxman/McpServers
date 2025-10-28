# get_audit_log

Get recent audit log entries for security and troubleshooting.

**Category:** [maintenance](INDEX.md)

---

## Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| count | integer | ✗ | 20 | Number of entries to return |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| entries | object[] | Array of audit entry objects |
| totalEntries | integer | Total entries in log |
| returned | integer | Entries in this response |

**Audit entry object:**
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

---

## Examples

**Recent activity:**
```
get_audit_log(count: 50)
→ Last 50 operations
```

**Full audit:**
```
get_audit_log(count: 1000)
→ Up to 1000 recent entries
```

---

## Notes

- **Use case:** Security audits, troubleshooting, compliance
- **Operations logged:** File operations, edits, process kills, security changes, commands
- **Retention:** See [INDEX.md](INDEX.md) for retention policy

---

## Related Tools

- [cleanup_backup_files](cleanup_backup_files.md) - Clean old backups
- [get_help](get_help.md) - Get system info