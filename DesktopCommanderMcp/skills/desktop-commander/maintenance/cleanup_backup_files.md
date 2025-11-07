# cleanup_backup_files

Clean up old backup files based on age and pattern.

**Category:** [maintenance](INDEX.md)  
**Common concepts:** [backup file format](../COMMON.md#backup-file-naming)

---

## Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| directoryPath | string | ✓ | - | Directory to clean |
| olderThanHours | integer | ✗ | 24 | Delete backups older than this |
| pattern | string | ✗ | "*.backup.*" | File pattern to match |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| directoryPath | string | Directory cleaned |
| filesDeleted | integer | Number of files deleted |
| mbFreed | number | Megabytes freed |
| errors | string[] | Errors (if any) |

---

## Examples

**Clean week-old backups:**
```
cleanup_backup_files(
  directoryPath: "C:\\Projects",
  olderThanHours: 168  // 7 days
)
```

**Clean specific pattern:**
```
cleanup_backup_files(
  directoryPath: "C:\\Projects\\MyApp",
  olderThanHours: 24,
  pattern: "*.cs.backup.*"
)
→ Only C# file backups
```

**Emergency cleanup (all backups):**
```
cleanup_backup_files(
  directoryPath: "C:\\Projects",
  olderThanHours: 0
)
→ Deletes ALL backup files
```

---

## Notes

- **Backup format:** See [../COMMON.md](../COMMON.md#backup-file-naming) for format
- **Safety:** Test with specific directory first
- **Scheduled:** Automate regular cleanup
- **Disk space:** Check mbFreed to see space recovered

---

## Related Tools

- [get_audit_log](get_audit_log.md) - Review audit logs
- [search_files](../file-operations/search_files.md) - Find backup files