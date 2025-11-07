# Set Expire

Sets or updates the expiration time for an existing key. After the specified number of seconds, the key will be automatically deleted by Redis.

## Parameters

- **key** (string, required): The key for which to set the expiration
- **expirySeconds** (integer, required): Number of seconds until the key expires (must be positive)

## Returns

Returns a JSON object confirming the expiration was set:

```json
{
  "success": true,
  "key": "session:abc123xyz",
  "expirySeconds": 3600,
  "expirySet": true,
  "message": "Expiration set to 3600 seconds"
}
```

If the key does not exist:

```json
{
  "success": false,
  "key": "nonexistent:key",
  "expirySet": false,
  "error": "Key does not exist"
}
```

If the operation fails:

```json
{
  "success": false,
  "error": "Failed to set expiration: Invalid expiry value"
}
```

## Example

Set a session to expire in 1 hour (3600 seconds):
```
key: session:abc123xyz
expirySeconds: 3600
```

Set cache to expire in 5 minutes (300 seconds):
```
key: cache:product:12345
expirySeconds: 300
```

Set a temporary lock for 30 seconds:
```
key: lock:critical_section
expirySeconds: 30
```

Set rate limit window to 1 minute (60 seconds):
```
key: ratelimit:user:1001
expirySeconds: 60
```

Set verification code to expire in 10 minutes (600 seconds):
```
key: verify:email:abc123
expirySeconds: 600
```

## Notes

- This command only works on existing keys. The key must exist before you can set an expiration.
- Setting a new expiration on a key that already has one will replace the old expiration time.
- To remove an expiration and make a key persist forever, use the PERSIST command (if available).
- The key will be automatically deleted when the TTL reaches zero.
