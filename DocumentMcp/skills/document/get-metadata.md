# Get Metadata

Retrieves metadata information from a document without extracting the full content. Includes properties like title, author, creation date, modification date, and document-specific properties.

## Parameters

- **filePath** (string, required): Full path to the document file

## Returns

Returns a JSON object with document metadata:
```json
{
  "success": true,
  "filePath": "/path/to/document.pdf",
  "metadata": {
    "title": "Document Title",
    "author": "Author Name",
    "subject": "Document Subject",
    "keywords": "keyword1, keyword2",
    "creator": "Microsoft Word",
    "producer": "PDF Library",
    "createdDate": "2025-01-01T10:00:00Z",
    "modifiedDate": "2025-01-15T14:30:00Z",
    "pageCount": 10,
    "fileSize": 1024000,
    "format": "PDF"
  }
}
```

## Example

```javascript
get_metadata({
  filePath: "C:\\Documents\\report.pdf"
})
```
