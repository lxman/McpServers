# List Indexes

Lists all available document indexes with their properties and statistics.

## Parameters

None

## Returns

Returns a JSON array of indexes:
```json
{
  "success": true,
  "indexes": [
    {
      "indexName": "reports_2024",
      "documentsIndexed": 10,
      "totalPages": 250,
      "totalWords": 125000,
      "indexSize": 5242880,
      "createdAt": "2025-11-06T10:30:00Z",
      "lastSearched": "2025-11-06T14:15:00Z",
      "searchCount": 25,
      "loaded": true
    }
  ],
  "totalCount": 1
}
```

## Example

```javascript
list_indexes()
```
