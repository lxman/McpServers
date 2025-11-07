# Get TTL

Gets the Time To Live (TTL) for a key, which is the remaining time in seconds before the key expires and is automatically deleted.

## Parameters

- **key** (string, required): The key whose TTL should be retrieved

## Returns

Returns a JSON object with the TTL information:

```json
{
  "success": true,
  "key": "session:abc123xyz",
  "ttl": 3456,
  "hasExpiry": true,
  "message": "Key expires in 3456 seconds"
}
```

If the key exists but has no expiry:

```json
{
  "success": true,
  "key": "user:1001:name",
  "ttl": -1,
  "hasExpiry": false,
  "message": "Key exists but has no expiry set"
}
```

If the key does not exist:

```json
{
  "success": true,
  "key": "nonexistent:key",
  "ttl": -2,
  "hasExpiry": false,
  "message": "Key does not exist"
}
```

If the operation fails:

```json
{
  "success": false,
  "error": "Failed to get TTL: Connection error"
}
```

## Example

Check session expiry:
```
key: session:abc123xyz
```

Check cache TTL:
```
key: cache:product:12345
```

Check rate limit window:
```
key: ratelimit:user:1001
```

Check temporary lock:
```
key: lock:resource:database
```

## TTL Values Explained

- **Positive number**: Remaining seconds until expiry
- **-1**: Key exists but has no expiration set (persists forever)
- **-2**: Key does not exist

This is useful for:
- Monitoring when cached data will expire
- Checking session validity
- Debugging expiration issues
- Implementing TTL-based logic
