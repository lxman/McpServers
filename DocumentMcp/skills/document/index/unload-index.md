# Unload Index

Unloads an index from memory to free up resources. The index data is preserved on disk and can be reloaded later.

## Parameters

- **indexName** (string, required): Name of the index to unload

## Returns

Returns a JSON object with unload result:
```json
{
  "success": true,
  "indexName": "reports_2024",
  "freedMemory": 5242880,
  "message": "Index unloaded successfully"
}
```

## Example

```javascript
unload_index({
  indexName: "reports_2024"
})
```
