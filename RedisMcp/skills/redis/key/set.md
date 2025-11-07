# Set

Sets the string value of a key. If the key already exists, the value is overwritten. Optionally, you can set an expiry time in seconds.

## Parameters

- **key** (string, required): The key to set
- **value** (string, required): The value to store
- **expirySeconds** (integer, optional): Time in seconds until the key expires and is automatically deleted

## Returns

Returns a JSON object confirming the operation:

```json
{
  "success": true,
  "key": "user:1001:name",
  "value": "John Doe",
  "expirySeconds": null,
  "message": "Key set successfully"
}
```

With expiry:

```json
{
  "success": true,
  "key": "session:abc123xyz",
  "value": "user_data_here",
  "expirySeconds": 3600,
  "message": "Key set successfully with 3600 seconds expiry"
}
```

If the operation fails:

```json
{
  "success": false,
  "error": "Failed to set key: Connection lost"
}
```

## Example

Set a user's name:
```
key: user:1001:name
value: John Doe
```

Set a session token with 1 hour expiry:
```
key: session:abc123xyz
value: {"userId":1001,"loginTime":"2025-01-15T10:30:00Z"}
expirySeconds: 3600
```

Set a cache entry with 5 minute expiry:
```
key: cache:product:12345
value: {"id":12345,"name":"Widget","price":29.99}
expirySeconds: 300
```

Set a feature flag:
```
key: feature:new_ui_enabled
value: true
```
