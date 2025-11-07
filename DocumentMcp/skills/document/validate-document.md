# Validate Document

Validates a document file to check if it's readable, not corrupted, and contains accessible content. Useful for verifying document integrity before processing.

## Parameters

- **filePath** (string, required): Full path to the document file to validate

## Returns

Returns a JSON object with validation results:
```json
{
  "success": true,
  "filePath": "/path/to/document.pdf",
  "valid": true,
  "readable": true,
  "encrypted": false,
  "format": "PDF",
  "pageCount": 10,
  "warnings": [],
  "errors": []
}
```

If validation fails:
```json
{
  "success": false,
  "filePath": "/path/to/document.pdf",
  "valid": false,
  "readable": false,
  "errors": ["Document is corrupted", "Unable to read page structure"]
}
```

## Example

```javascript
validate_document({
  filePath: "C:\\Documents\\report.pdf"
})
```
