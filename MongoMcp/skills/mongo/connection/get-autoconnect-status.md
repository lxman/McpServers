# Get Autoconnect Status

Retrieves the status of automatic connection initialization for configured profiles.

## Parameters

No parameters required.

## Returns

Returns a JSON object with autoconnect status:

```json
{
  "autoconnectEnabled": true,
  "autoconnectedProfiles": [
    {
      "profileName": "staging",
      "serverName": "staging",
      "status": "connected",
      "connectedAt": "2025-01-15T10:30:00Z"
    }
  ],
  "failedProfiles": []
}
```

## Example

Get autoconnect status:

```
(no parameters)
```
