# start_service

Start an MCP service.

**Category:** [service-management](INDEX.md)

---

## Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| serviceName | string | ✓ | - | Service name from configuration |
| workingDirectory | string | ✗ | null | Override working directory |
| forceInit | boolean | ✗ | false | Force kill existing instance |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| serviceId | string | Service identifier |
| processId | integer | Process ID |
| started | boolean | Whether service started |

---

## Examples

**Start service:**
```
start_service(serviceName: "playwright-server")
```

**With custom directory:**
```
start_service(
  serviceName: "custom-server",
  workingDirectory: "C:\\MCP\\custom"
)
```

**Force restart:**
```
start_service(
  serviceName: "playwright-server",
  forceInit: true
)
→ Kills existing instance first
```

---

## Notes

- **Configuration:** Service must be registered in MCP config
- **forceInit:** Use to restart hung services
- **Verification:** Use [get_service_status](get_service_status.md) to verify

---

## Related Tools

- [list_servers](list_servers.md) - See available services
- [get_service_status](get_service_status.md) - Check status
- [stop_service](stop_service.md) - Stop service