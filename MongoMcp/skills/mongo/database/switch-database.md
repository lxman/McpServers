# Switch Database

Changes the active database for the specified server connection.

## Parameters

- **serverName** (string, required): The name of the server connection
- **databaseName** (string, required): The name of the database to switch to

## Returns

Returns a JSON object confirming the database switch:

```json
{
  "success": true,
  "serverName": "local",
  "previousDatabase": "myapp",
  "currentDatabase": "test",
  "message": "Successfully switched database"
}
```

## Example

Switch to the test database:

```
serverName: "local"
databaseName: "test"
```
