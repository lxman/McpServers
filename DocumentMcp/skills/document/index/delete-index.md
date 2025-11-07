# Delete Index

Permanently deletes an index and optionally removes the associated index files from disk.

## Parameters

- **indexName** (string, required): Name of the index to delete
- **deleteFiles** (boolean, required): Whether to delete index files from disk

## Returns

Returns a JSON object with deletion result:
```json
{
  "success": true,
  "indexName": "reports_2024",
  "filesDeleted": true,
  "freedDiskSpace": 5242880,
  "message": "Index deleted successfully"
}
```

## Example

```javascript
// Delete index and keep files
delete_index({
  indexName: "old_reports",
  deleteFiles: false
})

// Delete index and remove files
delete_index({
  indexName: "temp_index",
  deleteFiles: true
})
```
