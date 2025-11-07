# Search Document

Searches for a specific term within a single document. Returns all occurrences with page numbers and context.

## Parameters

- **filePath** (string, required): Full path to the document file to search
- **searchTerm** (string, required): Text to search for
- **caseSensitive** (boolean, required): Whether the search should be case-sensitive
- **wholeWord** (boolean, required): Match whole words only
- **maxResults** (integer, required): Maximum number of results to return

## Returns

Returns a JSON object with search results:
```json
{
  "success": true,
  "filePath": "/path/to/document.pdf",
  "searchTerm": "important",
  "resultsCount": 12,
  "maxResults": 20,
  "results": [
    {
      "page": 3,
      "position": 156,
      "context": "...this is an important consideration...",
      "highlightedText": "important"
    }
  ]
}
```

## Example

```javascript
// Case-insensitive search
search_document({
  filePath: "C:\\Documents\\report.pdf",
  searchTerm: "important",
  caseSensitive: false,
  wholeWord: true,
  maxResults: 20
})

// Case-sensitive whole word search
search_document({
  filePath: "C:\\Documents\\technical.docx",
  searchTerm: "API",
  caseSensitive: true,
  wholeWord: true,
  maxResults: 50
})
```
