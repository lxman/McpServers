# Unload Document

Removes a document from memory and clears its cache. Use this to free up memory when a document is no longer needed.

## Parameters

- **filePath** (string, required): Full path to the document file to unload

## Returns

Returns a JSON object with the unload result:
```json
{
  "success": true,
  "filePath": "/path/to/document.pdf",
  "message": "Document unloaded successfully"
}
```

## Example

```javascript
unload_document({
  filePath: "C:\\Documents\\report.pdf"
})
```
