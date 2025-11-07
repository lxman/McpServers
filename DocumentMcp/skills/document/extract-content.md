# Extract Content

Extracts text content from a document. Supports extracting specific page ranges and includes optional metadata.

## Parameters

- **filePath** (string, required): Full path to the document file
- **includeMetadata** (boolean, required): Whether to include document metadata in the response
- **startPage** (integer, optional): Starting page number (1-based). If not specified, starts from first page
- **endPage** (integer, optional): Ending page number (1-based). If not specified, extracts to last page
- **maxPages** (integer, optional): Maximum number of pages to extract. Overrides endPage if specified

## Returns

Returns a JSON object with extracted content:
```json
{
  "success": true,
  "filePath": "/path/to/document.pdf",
  "content": "Extracted text content...",
  "pageCount": 10,
  "extractedPages": 5,
  "metadata": {
    "title": "Document Title",
    "author": "Author Name",
    "createdDate": "2025-01-01",
    "modifiedDate": "2025-01-15"
  }
}
```

## Example

```javascript
// Extract all content with metadata
extract_content({
  filePath: "C:\\Documents\\report.pdf",
  includeMetadata: true
})

// Extract specific page range
extract_content({
  filePath: "C:\\Documents\\report.pdf",
  includeMetadata: false,
  startPage: 1,
  endPage: 5
})

// Extract maximum 10 pages
extract_content({
  filePath: "C:\\Documents\\report.pdf",
  includeMetadata: true,
  maxPages: 10
})
```
