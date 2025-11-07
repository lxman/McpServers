# List Collections by Database

Retrieves a list of all collections in a specific database.

## Parameters

- **serverName** (string, required): The name of the server connection
- **databaseName** (string, required): The name of the database to list collections from

## Returns

Returns a JSON object with an array of collections:

```json
{
  "serverName": "local",
  "databaseName": "myapp",
  "collections": [
    {
      "name": "users",
      "type": "collection",
      "options": {},
      "info": {
        "count": 1500,
        "size": 3072000,
        "avgObjSize": 2048
      }
    },
    {
      "name": "orders",
      "type": "collection",
      "options": {},
      "info": {
        "count": 5000,
        "size": 10240000,
        "avgObjSize": 2048
      }
    }
  ],
  "count": 2
}
```

## Example

List collections in the myapp database:

```
serverName: "local"
databaseName: "myapp"
```
