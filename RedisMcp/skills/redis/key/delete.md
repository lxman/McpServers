# Delete

Removes the specified key and its associated value from Redis. This operation is immediate and irreversible.

## Parameters

- **key** (string, required): The key to delete

## Returns

Returns a JSON object indicating whether the key was deleted:

```json
{
  "success": true,
  "key": "user:1001:name",
  "deleted": true,
  "message": "Key deleted successfully"
}
```

If the key did not exist:

```json
{
  "success": true,
  "key": "nonexistent:key",
  "deleted": false,
  "message": "Key did not exist"
}
```

If the operation fails:

```json
{
  "success": false,
  "error": "Failed to delete key: Connection error"
}
```

## Example

Delete a user's cached data:
```
key: user:1001:cache
```

Delete an expired session:
```
key: session:old_token_xyz
```

Delete a temporary lock:
```
key: lock:resource:database
```

Delete a completed job:
```
key: job:12345:status
```

Note: This operation returns success even if the key doesn't exist. Check the `deleted` field to determine if a key was actually removed.
