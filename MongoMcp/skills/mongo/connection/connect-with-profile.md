# Connect with Profile

Establishes a connection using a saved connection profile from the configuration.

## Parameters

- **profileName** (string, required): The name of the connection profile to use

## Returns

Returns a JSON object with connection status:

```json
{
  "success": true,
  "serverName": "local-dev",
  "databaseName": "development",
  "profileName": "local-dev",
  "message": "Successfully connected using profile"
}
```

## Example

Connect using the staging profile:

```
profileName: "staging"
```
