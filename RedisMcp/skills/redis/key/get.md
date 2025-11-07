# Get

Retrieves the value stored at the specified key. This command works only for string values.

## Parameters

- **key** (string, required): The key whose value should be retrieved

## Returns

Returns a JSON object containing the value:

```json
{
  "success": true,
  "key": "user:1001:name",
  "value": "John Doe",
  "type": "string"
}
```

If the key does not exist:

```json
{
  "success": false,
  "key": "user:9999:name",
  "error": "Key not found"
}
```

If the key contains a non-string value (list, set, hash, etc.):

```json
{
  "success": false,
  "key": "users:list",
  "error": "WRONGTYPE Operation against a key holding the wrong kind of value"
}
```

## Example

Get a user's name:
```
key: user:1001:name
```

Get a session token:
```
key: session:abc123xyz
```

Get a cached value:
```
key: cache:homepage:html
```

Get a configuration setting:
```
key: config:max_connections
```
