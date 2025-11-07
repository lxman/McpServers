# Get Keys

Retrieves keys matching a specified pattern from the currently selected database. This uses the SCAN command for safe, non-blocking iteration.

## Parameters

- **pattern** (string, optional): A glob-style pattern to match keys. Default is `*` (all keys). Supports wildcards:
  - `*` matches any characters
  - `?` matches a single character
  - `[abc]` matches one of the characters in brackets
- **count** (integer, optional): Approximate number of keys to return per iteration (hint to Redis). Default is 100. Higher values may return more keys but take longer.

## Returns

Returns a JSON object with the matching keys:

```json
{
  "success": true,
  "pattern": "user:*",
  "keys": [
    "user:1001:name",
    "user:1001:email",
    "user:1002:name",
    "user:1003:name"
  ],
  "count": 4,
  "scanned": true
}
```

If no keys match:

```json
{
  "success": true,
  "pattern": "nonexistent:*",
  "keys": [],
  "count": 0,
  "scanned": true
}
```

## Example

Get all keys:
```
pattern: *
```

Get all user keys:
```
pattern: user:*
```

Get all session keys:
```
pattern: session:*
```

Get keys for a specific user:
```
pattern: user:1001:*
```

Get cache keys with pattern:
```
pattern: cache:product:*
```

Get keys with specific prefix:
```
pattern: config:database:?
```

Get more keys per scan:
```
pattern: user:*
count: 500
```

**Warning**: Be cautious when using patterns like `*` on large databases, as it may return thousands or millions of keys. Always use specific patterns when possible.
