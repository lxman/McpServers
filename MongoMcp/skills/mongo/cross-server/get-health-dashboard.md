# Get Health Dashboard

Retrieves a comprehensive health status dashboard for all connected MongoDB servers.

## Parameters

No parameters required.

## Returns

Returns a JSON object with health information for all servers:

```json
{
  "timestamp": "2025-01-15T10:30:00Z",
  "servers": [
    {
      "serverName": "local",
      "status": "healthy",
      "databaseName": "myapp",
      "metrics": {
        "uptime": 86400,
        "connections": {
          "current": 5,
          "available": 838855
        },
        "memory": {
          "resident": 52428800,
          "virtual": 104857600
        },
        "operations": {
          "insert": 1500,
          "query": 25000,
          "update": 500,
          "delete": 50
        },
        "responseTime": 2
      }
    },
    {
      "serverName": "production",
      "status": "healthy",
      "databaseName": "prod_db",
      "metrics": {
        "uptime": 172800,
        "connections": {
          "current": 125,
          "available": 838730
        },
        "memory": {
          "resident": 524288000,
          "virtual": 1048576000
        },
        "operations": {
          "insert": 50000,
          "query": 1000000,
          "update": 25000,
          "delete": 2000
        },
        "responseTime": 15
      }
    }
  ],
  "totalServers": 2,
  "healthyServers": 2,
  "unhealthyServers": 0
}
```

## Example

Get health dashboard for all servers:

```
(no parameters)
```
