# Process Management

Tools for listing, monitoring, and controlling system processes.

**See [../COMMON.md](../COMMON.md) for shared concepts: standard responses, error handling.**

---

## Tools Quick Reference

| Tool | Purpose | Key Parameters |
|------|---------|----------------|
| [list_processes](list_processes.md) | List running processes | filter?, limit? |
| [get_process_info](get_process_info.md) | Get detailed process info | processId |
| [kill_process](kill_process.md) | Terminate process by ID | processId, force? |
| [kill_process_by_name](kill_process_by_name.md) | Terminate all processes by name | processName, confirmation? |

---

## Common Workflows

### Find and Kill Process
```
1. list_processes(filter: "chrome")
   → Find Chrome processes

2. get_process_info(processId: 1234)
   → Verify it's the right process

3. kill_process(processId: 1234)
   → Terminate gracefully

4. If stuck: kill_process(processId: 1234, force: true)
   → Force termination
```

### Monitor Resource Usage
```
1. list_processes(limit: 50)
   → Get top 50 processes

2. For each high-memory process:
   get_process_info(processId)
   → Detailed memory, CPU, thread info
```

### Cleanup by Name
```
kill_process_by_name(
  processName: "node",
  confirmation: "KILL ALL"
)
→ Terminates all Node.js processes
```

---

## Process Information

### list_processes Response
```json
{
  "success": true,
  "processes": [
    {
      "processId": 1234,
      "processName": "chrome",
      "memoryUsageMB": 512.5,
      "cpuPercent": 15.3,
      "startTime": "2025-10-21T10:00:00Z"
    }
  ],
  "totalProcesses": 150,
  "returned": 50
}
```

### get_process_info Response
```json
{
  "success": true,
  "processId": 1234,
  "processName": "chrome.exe",
  "path": "C:\\Program Files\\Google\\Chrome\\chrome.exe",
  "commandLine": "chrome.exe --type=renderer",
  "memoryUsageMB": 512.5,
  "cpuPercent": 15.3,
  "threadCount": 24,
  "handleCount": 1250,
  "startTime": "2025-10-21T10:00:00Z",
  "parentProcessId": 5678,
  "priority": "Normal"
}
```

---

## Best Practices

### Finding Processes

1. **Use descriptive filters:**
    - ✓ Good: `filter: "chrome"`, `filter: "node"`
    - ✗ Too broad: `filter: "e"`, `filter: "s"`

2. **Set reasonable limits:**
    - Default: 50 processes
    - Adjust based on needs
    - Use filter to reduce results

3. **Case-insensitive matching:**
    - `"Chrome"` = `"chrome"` = `"CHROME"`

### Killing Processes

1. **Verify before killing:**
   ```
   1. list_processes to find candidates
   2. get_process_info to verify
   3. kill_process for specific ID
   ```

2. **Graceful vs Force:**
    - Try graceful termination first (`force: false`)
    - Use force only if process doesn't respond
    - Force may lose unsaved data

3. **Multiple instances:**
    - `kill_process`: One specific process
    - `kill_process_by_name`: All matching processes
    - Be careful with common names!

### Safety

1. **Critical processes:**
    - Don't kill system processes
    - Be careful with explorer.exe, winlogon.exe
    - Killing critical processes can crash system

2. **Confirmation for batch kills:**
   ```
   kill_process_by_name requires confirmation: "KILL ALL"
   → Prevents accidental mass termination
   ```

3. **Check process path:**
    - Verify it's the intended application
    - Some processes share names
    - Use full path to differentiate

---

## Common Use Cases

### Development Cleanup
```
Kill all Node.js development servers:
  kill_process_by_name(processName: "node", confirmation: "KILL ALL")

Kill specific port-bound process:
  1. list_processes(filter: "node")
  2. get_process_info for each
  3. Find one using port 3000
  4. kill_process(processId)
```

### Browser Management
```
Close all browser instances:
  kill_process_by_name(processName: "chrome", confirmation: "KILL ALL")

Close specific browser window:
  1. list_processes(filter: "chrome")
  2. Identify by memory/start time
  3. kill_process(processId)
```

### Resource Investigation
```
Find memory hogs:
  1. list_processes(limit: 100)
  2. Sort by memoryUsageMB
  3. get_process_info for top consumers
  4. Decide whether to kill
```

### Stuck Application
```
1. list_processes(filter: "app-name")
2. kill_process(processId, force: false)
3. If still running:
   kill_process(processId, force: true)
```

---

## Process Priorities

### Priority Levels
- **Realtime:** Highest (use carefully)
- **High:** Above normal
- **AboveNormal:** Slightly elevated
- **Normal:** Standard (default)
- **BelowNormal:** Lower priority
- **Idle:** Lowest (background tasks)

### When to Check Priority
```
get_process_info(processId)
→ Check priority field
→ High-priority processes may impact system
```

---

## Security Considerations

1. **Process permissions:**
    - May need admin rights for some processes
    - Can't kill protected system processes
    - Respect process ownership

2. **Confirmation requirements:**
    - `kill_process_by_name` requires explicit confirmation
    - Prevents accidental batch termination
    - Use exact string: "KILL ALL"

3. **Audit logging:**
    - All kill operations logged
    - Review [maintenance/get_audit_log](../maintenance/get_audit_log.md)
    - Track who killed what and when

---

## Performance Tips

1. **Filtering:**
    - Use specific filters to reduce results
    - Faster than retrieving all processes
    - Less data transfer

2. **Limits:**
    - Set appropriate limits
    - Don't request more than needed
    - Default (50) is usually sufficient

3. **Batch operations:**
    - Use `kill_process_by_name` for multiple instances
    - Faster than killing individually
    - Single operation vs. multiple calls

---

## Troubleshooting

### Access Denied
```
Issue: "Access denied" when killing process
Solutions:
  - Run Desktop Commander as administrator
  - Check if process is protected
  - Verify process ownership
```

### Process Not Found
```
Issue: Process disappeared between list and kill
Solutions:
  - Process exited normally
  - Another process killed it
  - Re-run list_processes to verify
```

### Force Kill Failed
```
Issue: Process won't die even with force: true
Solutions:
  - Process may be protected/system-critical
  - Try using Task Manager
  - Restart system if absolutely necessary
```

### Wrong Process Killed
```
Prevention:
  1. Always verify with get_process_info
  2. Check process path and command line
  3. Don't rely solely on process name
  4. Use process ID for precision
```

---

## Monitoring Strategies

### Periodic Monitoring
```
Every 5 seconds:
  1. list_processes(limit: 20)
  2. Check CPU % and memory
  3. Alert if thresholds exceeded
```

### Resource Alerts
```
1. list_processes()
2. For each process:
   - If memoryUsageMB > 1000: Alert
   - If cpuPercent > 80: Alert
3. Get detailed info and log
```

### Process Lifecycle
```
1. Record startTime from get_process_info
2. Calculate uptime
3. Monitor for unexpected restarts
4. Track resource usage over time
```

---

**Total Tools:** 4  
**See [../INDEX.md](../INDEX.md) for complete Desktop Commander reference**