# Get Document Status

Retrieves the current status of the document processing system, including loaded documents, memory usage, and cache statistics.

## Parameters

None

## Returns

Returns a JSON object with system status:
```json
{
  "success": true,
  "loadedDocuments": 5,
  "cachedDocuments": 3,
  "memoryUsage": {
    "used": 52428800,
    "total": 536870912,
    "percentage": 9.76
  },
  "cacheSize": 10485760,
  "uptime": "2h 30m 15s",
  "processedDocuments": 25
}
```

## Example

```javascript
get_document_status()
```
