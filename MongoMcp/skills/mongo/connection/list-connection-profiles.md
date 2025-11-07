# List Connection Profiles

Retrieves all saved connection profiles from the configuration.

## Parameters

No parameters required.

## Returns

Returns a JSON object with an array of connection profiles:

```json
{
  "profiles": [
    {
      "name": "local-dev",
      "connectionString": "mongodb://localhost:27017",
      "databaseName": "development",
      "autoConnect": false
    },
    {
      "name": "staging",
      "connectionString": "mongodb+srv://user@staging.mongodb.net",
      "databaseName": "staging_db",
      "autoConnect": true
    }
  ],
  "count": 2
}
```

## Example

List all connection profiles:

```
(no parameters)
```
