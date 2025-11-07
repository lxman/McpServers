# Check Scanned PDF

Analyzes a PDF document to determine if it's a scanned document (image-based) or contains native text. Useful for deciding whether OCR processing is needed.

## Parameters

- **filePath** (string, required): Full path to the PDF file to check

## Returns

Returns a JSON object with scan detection results:
```json
{
  "success": true,
  "filePath": "/path/to/document.pdf",
  "isScanned": true,
  "textPages": 2,
  "imagePages": 8,
  "totalPages": 10,
  "ocrRequired": true,
  "confidence": 0.95,
  "recommendation": "Document appears to be scanned. OCR recommended for pages 3-10."
}
```

## Example

```javascript
check_scanned_pdf({
  filePath: "C:\\Documents\\scanned_report.pdf"
})
```
