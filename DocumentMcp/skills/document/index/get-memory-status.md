# Get Memory Status

Retrieves detailed memory usage information for the indexing system, including per-index memory consumption.

## Parameters

None

## Returns

Returns a JSON object with memory status:
```json
{
  "success": true,
  "totalMemoryUsed": 52428800,
  "availableMemory": 484442112,
  "memoryPercentage": 9.76,
  "indexes": [
    {
      "indexName": "reports_2024",
      "memoryUsed": 5242880,
      "loaded": true
    }
  ],
  "cacheMemory": 10485760,
  "systemMemory": {
    "total": 536870912,
    "used": 268435456,
    "free": 268435456
  }
}
```

## Example

```javascript
get_memory_status()
```
