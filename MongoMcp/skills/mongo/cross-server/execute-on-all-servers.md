# Execute on All Servers

Executes the same database command on all currently connected servers.

## Parameters

- **command** (string, required): JSON string representing the database command to execute on each server

## Returns

Returns a JSON object with execution results from all servers:

```json
{
  "command": "{\"ping\": 1}",
  "results": [
    {
      "serverName": "local",
      "success": true,
      "result": {
        "ok": 1
      },
      "duration": 5
    },
    {
      "serverName": "staging",
      "success": true,
      "result": {
        "ok": 1
      },
      "duration": 12
    },
    {
      "serverName": "production",
      "success": true,
      "result": {
        "ok": 1
      },
      "duration": 8
    }
  ],
  "totalServers": 3,
  "successful": 3,
  "failed": 0
}
```

## Example

Ping all servers:

```
command: "{\"ping\": 1}"
```

Get server status from all servers:

```
command: "{\"serverStatus\": 1}"
```

Get database stats from all servers:

```
command: "{\"dbStats\": 1}"
```
