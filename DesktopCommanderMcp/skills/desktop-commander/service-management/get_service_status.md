# get_service_status

Get detailed status and metrics for an MCP service.

**Category:** [service-management](INDEX.md)

---

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| serviceId | string | ✓ | Service identifier |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| serviceId | string | Service identifier |
| status | string | "Running", "Stopped", "Error" |
| processId | integer | Process ID (if running) |
| startTime | string | Start time (ISO 8601) |
| uptime | string | Human-readable uptime |
| memoryUsageMB | number | Memory usage |
| workingDirectory | string | Working directory |
| lastHealthCheck | string | Last health check time |
| healthStatus | string | "Healthy", "Unhealthy" |
| requestCount | integer | Total requests processed |
| errorCount | integer | Total errors |

---

## Example

```
get_service_status(serviceId: "playwright-server")
→ Detailed status and performance metrics
```

---

## Notes

- **Health:** Check regularly for service health monitoring
- **Metrics:** Track performance over time

---

## Related Tools

- [list_servers](list_servers.md) - List all servers
- [start_service](start_service.md) - Start if stopped
- [stop_service](stop_service.md) - Stop if needed