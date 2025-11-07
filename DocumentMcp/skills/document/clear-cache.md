# Clear Cache

Clears all cached documents from memory. This frees up memory but requires documents to be reloaded for subsequent operations.

## Parameters

None

## Returns

Returns a JSON object with cache clearing results:
```json
{
  "success": true,
  "clearedCount": 5,
  "freedMemory": 10485760,
  "message": "Cache cleared successfully"
}
```

## Example

```javascript
clear_cache()
```
