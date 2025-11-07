# Load Document

Loads a document into memory for processing. Supports PDF, DOCX, XLSX, PPTX, RTF, ODT, HTML, and other document formats. The document can be cached for faster subsequent access.

## Parameters

- **filePath** (string, required): Full path to the document file to load
- **password** (string, optional): Password for encrypted/protected documents
- **cache** (boolean, optional): Whether to cache the document for faster subsequent access. Default: true

## Returns

Returns a JSON object with the loading result:
```json
{
  "success": true,
  "filePath": "/path/to/document.pdf",
  "pageCount": 10,
  "fileSize": 1024000,
  "format": "PDF",
  "cached": true,
  "message": "Document loaded successfully"
}
```

## Example

```javascript
// Load a PDF document with caching
load_document({
  filePath: "C:\\Documents\\report.pdf",
  cache: true
})

// Load password-protected document
load_document({
  filePath: "C:\\Documents\\confidential.pdf",
  password: "secret123",
  cache: false
})
```
