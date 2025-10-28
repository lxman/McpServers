# list_servers

Get directory of all registered MCP servers.

**Category:** [service-management](INDEX.md)

---

## Parameters

None

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| servers | object[] | Array of server objects |
| totalServers | integer | Number of registered servers |
| runningCount | integer | Number currently running |

**Server object:**
```json
{
  "serviceId": "playwright-server",
  "name": "Playwright Server",
  "status": "Running",
  "capabilities": ["browser-automation", "testing"],
  "version": "1.0.0",
  "startTime": "2025-10-21T10:00:00Z",
  "uptime": "2h 15m"
}
```

---

## Example

```
list_servers()
→ Shows all registered MCP servers and their status
```

---

## Notes

- **Status:** "Running", "Stopped", "Error"
- **Use case:** Discover available services, check status

---

## Related Tools

- [get_service_status](get_service_status.md) - Detailed status
- [start_service](start_service.md) - Start server
- [stop_service](stop_service.md) - Stop server