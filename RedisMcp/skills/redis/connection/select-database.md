# Select Database

Selects a specific Redis database by its numeric index. Redis supports multiple databases (typically 0-15 by default), allowing logical separation of data.

## Parameters

- **databaseNumber** (integer, required): The database index to select (typically 0-15, default is 0)

## Returns

Returns a JSON object confirming the database selection:

```json
{
  "success": true,
  "message": "Selected database 2",
  "previousDatabase": 0,
  "currentDatabase": 2
}
```

If the selection fails:

```json
{
  "success": false,
  "error": "Invalid database number: 16"
}
```

## Example

Select database 0 (default database):
```
databaseNumber: 0
```

Select database 5 for testing data:
```
databaseNumber: 5
```

Select database 1 for session storage:
```
databaseNumber: 1
```

Note: The number of available databases is configured in the Redis server configuration file (redis.conf) using the `databases` directive. The default is 16 databases (0-15).
