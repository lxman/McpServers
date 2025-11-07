# Service Management

Manage MCP (Model Context Protocol) services - start, stop, monitor, and configure.

**See [../COMMON.md](../COMMON.md) for shared concepts: standard responses, error handling.**

---

## Tools Quick Reference

| Tool | Purpose | Key Parameters |
|------|---------|----------------|
| [list_servers](list_servers.md) | Get MCP server directory | (none) |
| [get_service_status](get_service_status.md) | Get service status and metrics | serviceId |
| [start_service](start_service.md) | Start MCP service | serviceName, workingDirectory?, forceInit? |
| [stop_service](stop_service.md) | Stop MCP service | serviceId |
| [reload_server_registry](reload_server_registry.md) | Reload service configuration | (none) |

---

## MCP Service Concepts

### What is an MCP Service?

An MCP (Model Context Protocol) service is:
- A server that provides tools/capabilities
- Runs as separate process
- Communicates via MCP protocol
- Managed by Desktop Commander

**Examples:**
- Playwright automation server
- Database integration server
- Custom tool server
- Third-party MCP servers

### Service Lifecycle

```
1. Service Registered
   → In configuration file
   → Available but not running

2. start_service
   → Process spawned
   → Initialization begins
   → Status: Starting

3. Service Ready
   → Initialization complete
   → Accepting requests
   → Status: Running

4. stop_service
   → Graceful shutdown
   → Cleanup resources
   → Status: Stopped
```

---

## Common Workflows

### Check Available Services
```
list_servers()
→ Returns all registered MCP servers
→ Shows status (running/stopped)
→ Displays capabilities
```

### Start a Service
```
start_service(
  serviceName: "playwright-server",
  workingDirectory: "C:\\MCP\\playwright"
)
→ Service starts
→ Returns serviceId
```

### Monitor Service Health
```
get_service_status(serviceId: "playwright-server")
→ Status: Running/Stopped/Error
→ Uptime, memory usage
→ Error details if failed
```

### Stop a Service
```
stop_service(serviceId: "playwright-server")
→ Graceful shutdown
→ Resources cleaned up
```

### Reload Configuration
```
After editing MCP config file:
  reload_server_registry()
  → Picks up config changes
  → New services available
  → Old services unaffected
```

---

## Service Information

### list_servers Response

```json
{
  "success": true,
  "servers": [
    {
      "serviceId": "playwright-server",
      "name": "Playwright Server",
      "status": "Running",
      "capabilities": ["browser-automation", "testing"],
      "version": "1.0.0",
      "startTime": "2025-10-21T10:00:00Z",
      "uptime": "2h 15m"
    },
    {
      "serviceId": "db-server",
      "name": "Database Server",
      "status": "Stopped",
      "capabilities": ["database", "queries"],
      "version": "2.1.0"
    }
  ],
  "totalServers": 2,
  "runningCount": 1
}
```

### get_service_status Response

```json
{
  "success": true,
  "serviceId": "playwright-server",
  "status": "Running",
  "processId": 12345,
  "startTime": "2025-10-21T10:00:00Z",
  "uptime": "2h 15m 30s",
  "memoryUsageMB": 245.7,
  "workingDirectory": "C:\\MCP\\playwright",
  "lastHealthCheck": "2025-10-21T12:15:00Z",
  "healthStatus": "Healthy",
  "requestCount": 1523,
  "errorCount": 3
}
```

---

## Best Practices

### Service Starting

1. **Check status first:**
   ```
   1. list_servers()
   2. Check if already running
   3. start_service only if stopped
   ```

2. **Specify working directory:**
   ```
   start_service(
     serviceName: "custom-server",
     workingDirectory: "C:\\MCP\\custom"
   )
   → Ensures correct environment
   ```

3. **Handle startup failures:**
   ```
   result = start_service(...)
   if not result.success:
       log_error(result.error)
       check_configuration()
   ```

4. **Use forceInit carefully:**
   ```
   forceInit: true
   → Kills existing instance if running
   → Use only when necessary
   ```

### Service Monitoring

1. **Regular health checks:**
   ```
   Every 5 minutes:
     get_service_status(serviceId)
     if status != "Running":
         alert_and_restart()
   ```

2. **Track metrics:**
    - Memory usage trends
    - Request count patterns
    - Error rates
    - Uptime statistics

3. **Watch for issues:**
    - High memory usage
    - Increasing error count
    - Slow response times
    - Unexpected restarts

### Service Stopping

1. **Graceful shutdown:**
   ```
   stop_service(serviceId)
   → Allows cleanup
   → Waits for pending requests
   → Closes connections properly
   ```

2. **Verify stopped:**
   ```
   get_service_status(serviceId)
   → Confirm status: "Stopped"
   ```

3. **Handle errors:**
    - Service may not stop immediately
    - Wait and retry if needed
    - Force kill as last resort

---

## Configuration Management

### MCP Configuration File

Location: Typically in Desktop Commander root or config directory

**Format:**
```json
{
  "mcpServers": {
    "playwright-server": {
      "command": "node",
      "args": ["server.js"],
      "workingDirectory": "C:\\MCP\\playwright",
      "env": {
        "NODE_ENV": "production"
      }
    },
    "custom-server": {
      "command": "python",
      "args": ["server.py"],
      "workingDirectory": "C:\\MCP\\custom"
    }
  }
}
```

### Adding New Services

1. **Edit configuration file:**
    - Add service entry
    - Specify command and arguments
    - Set working directory
    - Configure environment

2. **Reload configuration:**
   ```
   reload_server_registry()
   → Desktop Commander reads config
   → New service available
   ```

3. **Start service:**
   ```
   start_service(serviceName: "new-service")
   ```

### Removing Services

1. **Stop service if running:**
   ```
   stop_service(serviceId: "old-service")
   ```

2. **Edit configuration:**
    - Remove service entry

3. **Reload:**
   ```
   reload_server_registry()
   ```

---

## Common Use Cases

### Development Environment Setup
```
Startup script:
  1. list_servers()
  2. For each required service:
     - start_service if stopped
  3. Verify all running
  4. Begin development work
```

### Service Health Monitoring
```
Monitoring loop:
  1. get_service_status for each service
  2. Check memory usage < threshold
  3. Verify error count not increasing
  4. Alert if unhealthy
  5. Auto-restart if needed
```

### Configuration Updates
```
After config changes:
  1. stop_service for affected services
  2. reload_server_registry()
  3. start_service with new config
  4. Verify services healthy
```

### Troubleshooting Failed Service
```
Service won't start:
  1. get_service_status → Check error
  2. Review working directory
  3. Check command and arguments
  4. Verify dependencies installed
  5. Check logs
  6. Fix issues and retry
```

---

## Security Considerations

1. **Service isolation:**
    - Services run as separate processes
    - Limited to configured permissions
    - Cannot access outside working directory

2. **Command execution:**
    - Only registered services can start
    - Cannot execute arbitrary commands
    - Configuration controlled by admin

3. **Resource limits:**
    - Monitor memory usage
    - Set reasonable timeouts
    - Prevent resource exhaustion

4. **Audit logging:**
    - All start/stop operations logged
    - Configuration changes tracked
    - Review with [maintenance/get_audit_log](../maintenance/get_audit_log.md)

---

## Performance Tips

1. **Start only needed services:**
    - Don't start all services at once
    - Start on-demand when needed
    - Stop when idle

2. **Monitor resource usage:**
    - Track memory over time
    - Restart if memory leak detected
    - Set alerts for thresholds

3. **Optimize startup:**
    - Use specific working directory
    - Minimize initialization tasks
    - Cache when possible

---

## Troubleshooting

### Service Won't Start
```
Issue: start_service fails
Solutions:
  - Check service is registered (list_servers)
  - Verify working directory exists
  - Check command is valid
  - Review configuration syntax
  - Check dependencies installed
  - Review error message details
```

### Service Crashes Repeatedly
```
Issue: Service starts then immediately stops
Solutions:
  - get_service_status for error details
  - Check application logs
  - Verify all dependencies present
  - Check for port conflicts
  - Review resource constraints
```

### Configuration Not Updating
```
Issue: reload_server_registry doesn't pick up changes
Solutions:
  - Verify configuration file syntax (valid JSON)
  - Check file path is correct
  - Restart Desktop Commander if needed
  - Review error messages
```

### High Memory Usage
```
Issue: Service consuming excessive memory
Solutions:
  - Check for memory leaks
  - Restart service periodically
  - Optimize service code
  - Set memory limits if possible
```

---

## Advanced Patterns

### Service Dependencies
```
Start services in order:
  1. start_service("database-server")
  2. Wait for healthy status
  3. start_service("api-server")
  4. Wait for healthy status
  5. start_service("web-server")
```

### Health Check Automation
```
Watchdog process:
  Every N minutes:
    for each critical service:
      status = get_service_status(service)
      if status.health != "Healthy":
        stop_service(service)
        start_service(service)
        alert_admin()
```

### Load Balancing
```
Multiple instances:
  start_service("worker-1")
  start_service("worker-2")
  start_service("worker-3")
  
Monitor and balance load across instances
```

---

**Total Tools:** 5  
**See [../INDEX.md](../INDEX.md) for complete Desktop Commander reference**