# list_processes

List running processes with filtering.

**Category:** [process-management](INDEX.md)

---

## Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| filter | string | ✗ | null | Filter by process name (partial match) |
| limit | integer | ✗ | 50 | Maximum processes to return |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| processes | object[] | Array of process objects |
| totalProcesses | integer | Total matching processes |
| returned | integer | Processes in this response |

**Process object:**
```json
{
  "processId": 1234,
  "processName": "chrome",
  "memoryUsageMB": 512.5,
  "cpuPercent": 15.3,
  "startTime": "2025-10-21T10:00:00Z"
}
```

---

## Examples

**List all processes (top 50):**
```
list_processes()
```

**Filter by name:**
```
list_processes(filter: "chrome", limit: 20)
→ Returns Chrome processes only
```

**Find node processes:**
```
list_processes(filter: "node")
```

---

## Notes

- **Case-insensitive:** Filter matches regardless of case
- **Partial match:** "chrome" matches "chrome.exe", "chrome helper"
- **Sorted:** Typically by memory usage (highest first)
- **Use case:** Process discovery, resource monitoring

---

## Related Tools

- [get_process_info](get_process_info.md) - Detailed process info
- [kill_process](kill_process.md) - Terminate specific process
- [kill_process_by_name](kill_process_by_name.md) - Terminate all matching