# Create Index

Creates a searchable index from one or more documents. The index enables fast full-text search across document contents.

## Parameters

- **indexName** (string, required): Unique name for the index
- **documents** (array, required): Array of document file paths to include in the index
- **configuration** (object, optional): Index configuration options:
  - `caseSensitive` (boolean): Whether search should be case-sensitive. Default: false
  - `stemming` (boolean): Enable word stemming for better search results. Default: true
  - `stopWords` (boolean): Remove common stop words. Default: true
  - `language` (string): Index language for stemming. Default: "english"

## Returns

Returns a JSON object with index creation results:
```json
{
  "success": true,
  "indexName": "my_documents",
  "documentsIndexed": 10,
  "totalPages": 250,
  "totalWords": 125000,
  "indexSize": 5242880,
  "createdAt": "2025-11-06T10:30:00Z",
  "message": "Index created successfully"
}
```

## Example

```javascript
// Create basic index
create_index({
  indexName: "reports_2024",
  documents: [
    "C:\\Documents\\report1.pdf",
    "C:\\Documents\\report2.pdf",
    "C:\\Documents\\report3.pdf"
  ]
})

// Create index with custom configuration
create_index({
  indexName: "technical_docs",
  documents: [
    "C:\\Docs\\manual.pdf",
    "C:\\Docs\\guide.docx"
  ],
  configuration: {
    caseSensitive: true,
    stemming: true,
    stopWords: false,
    language: "english"
  }
})
```
