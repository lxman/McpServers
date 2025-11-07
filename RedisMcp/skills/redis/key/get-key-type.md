# Get Key Type

Determines the data type of the value stored at the specified key.

## Parameters

- **key** (string, required): The key whose type should be determined

## Returns

Returns a JSON object containing the key's type:

```json
{
  "success": true,
  "key": "user:1001:name",
  "type": "string"
}
```

Possible type values:
- `string`: Simple string value
- `list`: List of strings
- `set`: Unordered set of unique strings
- `zset`: Sorted set with scores
- `hash`: Hash map (field-value pairs)
- `stream`: Redis stream
- `none`: Key does not exist

If the key does not exist:

```json
{
  "success": true,
  "key": "nonexistent:key",
  "type": "none"
}
```

If the operation fails:

```json
{
  "success": false,
  "error": "Failed to get key type: Connection error"
}
```

## Example

Check the type of a user field:
```
key: user:1001:name
```
Returns: `string`

Check the type of a list:
```
key: notifications:1001
```
Returns: `list`

Check the type of a hash:
```
key: user:1001:profile
```
Returns: `hash`

Check the type of a sorted set:
```
key: leaderboard:global
```
Returns: `zset`

This is useful for:
- Determining which Redis commands are appropriate for a key
- Validating data structure expectations
- Debugging unexpected data types
- Building generic Redis inspection tools
