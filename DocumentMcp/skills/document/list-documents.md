# List Documents

Lists all currently loaded documents in memory with their status and basic information.

## Parameters

None

## Returns

Returns a JSON array of loaded documents:
```json
{
  "documents": [
    {
      "filePath": "/path/to/document.pdf",
      "format": "PDF",
      "pageCount": 10,
      "fileSize": 1024000,
      "cached": true,
      "loadedAt": "2025-11-06T10:30:00Z"
    }
  ],
  "totalCount": 1
}
```

## Example

```javascript
list_documents()
```
