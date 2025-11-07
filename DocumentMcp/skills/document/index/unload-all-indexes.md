# Unload All Indexes

Unloads all indexes from memory to free up resources. Index data is preserved on disk and can be reloaded later.

## Parameters

None

## Returns

Returns a JSON object with unload results:
```json
{
  "success": true,
  "unloadedCount": 3,
  "freedMemory": 15728640,
  "message": "All indexes unloaded successfully"
}
```

## Example

```javascript
unload_all_indexes()
```
