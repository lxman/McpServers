# Execute Command

Executes a raw database command on the specified server for advanced operations.

## Parameters

- **serverName** (string, required): The name of the server connection
- **command** (string, required): JSON string representing the database command to execute

## Returns

Returns a JSON object with the command execution result:

```json
{
  "success": true,
  "serverName": "local",
  "result": {
    "ok": 1,
    "version": "7.0.4",
    "gitVersion": "38f3e37057a43d2e9f41a39142681a26",
    "modules": [],
    "allocator": "tcmalloc",
    "javascriptEngine": "mozjs"
  }
}
```

## Example

Get server build info:

```
serverName: "local"
command: "{\"buildInfo\": 1}"
```

Get collection stats:

```
serverName: "local"
command: "{\"collStats\": \"users\"}"
```

Create a capped collection:

```
serverName: "local"
command: "{\"create\": \"logs\", \"capped\": true, \"size\": 10485760, \"max\": 5000}"
```
