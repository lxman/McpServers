# List Databases

Retrieves a list of all databases on the specified MongoDB server.

## Parameters

- **serverName** (string, required): The name of the server to list databases from

## Returns

Returns a JSON object with an array of databases:

```json
{
  "serverName": "local",
  "databases": [
    {
      "name": "admin",
      "sizeOnDisk": 73728,
      "empty": false
    },
    {
      "name": "myapp",
      "sizeOnDisk": 8192000,
      "empty": false
    },
    {
      "name": "test",
      "sizeOnDisk": 32768,
      "empty": false
    }
  ],
  "totalSize": 8298496,
  "count": 3
}
```

## Example

List databases on the local server:

```
serverName: "local"
```
