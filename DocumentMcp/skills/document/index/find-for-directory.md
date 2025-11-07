# Find Index for Directory

Finds all indexes that contain documents from a specific directory. Useful for locating relevant indexes when working with document collections.

## Parameters

- **directoryPath** (string, required): Full path to the directory to search for

## Returns

Returns a JSON object with matching indexes:
```json
{
  "success": true,
  "directoryPath": "C:\\Documents\\Reports",
  "matchingIndexes": [
    {
      "indexName": "reports_2024",
      "documentsFromDirectory": 8,
      "totalDocuments": 10,
      "percentage": 80
    }
  ],
  "totalMatches": 1
}
```

## Example

```javascript
find_for_directory({
  directoryPath: "C:\\Documents\\Reports"
})
```
