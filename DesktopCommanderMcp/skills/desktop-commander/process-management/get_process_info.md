# get_process_info

Get detailed information about a specific process.

**Category:** [process-management](INDEX.md)

---

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| processId | integer | ✓ | Process ID |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| processId | integer | Process ID |
| processName | string | Process name (e.g., "chrome.exe") |
| path | string | Full executable path |
| commandLine | string | Command line arguments |
| memoryUsageMB | number | Memory usage in MB |
| cpuPercent | number | CPU usage percentage |
| threadCount | integer | Number of threads |
| handleCount | integer | Number of handles |
| startTime | string | Start time (ISO 8601) |
| parentProcessId | integer | Parent process ID |
| priority | string | Process priority |

---

## Example

```
get_process_info(processId: 1234)
→ {
    "processName": "node.exe",
    "path": "C:\\Program Files\\nodejs\\node.exe",
    "memoryUsageMB": 245.7,
    "cpuPercent": 8.5,
    "threadCount": 12
  }
```

---

## Notes

- **Use case:** Verify process identity before killing, resource monitoring
- **Not found:** Returns success: false if process doesn't exist
- **Follow-up:** Use before [kill_process](kill_process.md)

---

## Related Tools

- [list_processes](list_processes.md) - Find process ID
- [kill_process](kill_process.md) - Terminate process