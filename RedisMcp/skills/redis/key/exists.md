# Exists

Checks whether a key exists in the currently selected Redis database.

## Parameters

- **key** (string, required): The key to check for existence

## Returns

Returns a JSON object indicating whether the key exists:

```json
{
  "success": true,
  "key": "user:1001:name",
  "exists": true
}
```

If the key does not exist:

```json
{
  "success": true,
  "key": "user:9999:name",
  "exists": false
}
```

If the operation fails:

```json
{
  "success": false,
  "error": "Failed to check key existence: Connection error"
}
```

## Example

Check if a user exists:
```
key: user:1001:profile
```

Check if a cache entry is present:
```
key: cache:homepage:html
```

Check if a lock is held:
```
key: lock:critical_section
```

Check if a session is active:
```
key: session:abc123xyz
```

This operation is useful for:
- Verifying a key exists before attempting to read it
- Implementing conditional logic based on key presence
- Checking locks before acquiring them
- Validating session tokens
