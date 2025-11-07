# Compare Documents

Compares two documents and identifies differences. Supports multiple comparison types including content comparison, structural comparison, and metadata comparison.

## Parameters

- **filePath1** (string, required): Full path to the first document file
- **filePath2** (string, required): Full path to the second document file
- **comparisonType** (string, required): Type of comparison to perform. Options:
  - `content` - Compare text content
  - `structure` - Compare document structure (pages, sections)
  - `metadata` - Compare document metadata
  - `full` - Perform all comparison types

## Returns

Returns a JSON object with comparison results:
```json
{
  "success": true,
  "filePath1": "/path/to/document1.pdf",
  "filePath2": "/path/to/document2.pdf",
  "comparisonType": "content",
  "identical": false,
  "similarityScore": 0.85,
  "differences": [
    {
      "type": "content",
      "location": "Page 3",
      "description": "Text changed from 'old text' to 'new text'"
    }
  ],
  "summary": "Documents are 85% similar with 5 differences found"
}
```

## Example

```javascript
// Compare content only
compare_documents({
  filePath1: "C:\\Documents\\report_v1.pdf",
  filePath2: "C:\\Documents\\report_v2.pdf",
  comparisonType: "content"
})

// Full comparison
compare_documents({
  filePath1: "C:\\Documents\\original.docx",
  filePath2: "C:\\Documents\\modified.docx",
  comparisonType: "full"
})
```
