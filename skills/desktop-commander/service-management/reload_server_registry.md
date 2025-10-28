# reload_server_registry

Reload MCP server configuration from config file.

**Category:** [service-management](INDEX.md)

---

## Parameters

None

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| serversLoaded | integer | Number of servers in config |
| newServers | string[] | Newly added server names |
| removedServers | string[] | Removed server names |

---

## Example

```
1. Edit MCP configuration file (add/remove servers)
2. reload_server_registry()
   → Configuration reloaded
3. list_servers()
   → See updated server list
```

---

## Notes

- **When to use:** After editing MCP configuration file
- **Running services:** Unaffected by reload
- **New services:** Available immediately after reload
- **Configuration file:** Typically in Desktop Commander root directory

---

## Related Tools

- [list_servers](list_servers.md) - See available servers
- [start_service](start_service.md) - Start newly added servers