# stop_service

Stop a running MCP service.

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
| serviceId | string | Service that was stopped |
| stopped | boolean | Whether service stopped |

---

## Example

```
stop_service(serviceId: "playwright-server")
→ Gracefully stops the service
```

---

## Notes

- **Graceful shutdown:** Allows cleanup and pending requests
- **Verification:** Use [get_service_status](get_service_status.md) to confirm stopped
- **Stuck services:** Use [start_service](start_service.md) with forceInit if needed

---

## Related Tools

- [list_servers](list_servers.md) - See running services
- [get_service_status](get_service_status.md) - Verify stopped
- [start_service](start_service.md) - Restart service