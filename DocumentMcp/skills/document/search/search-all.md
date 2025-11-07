# Search All Documents

Searches for a term across all currently loaded documents. Returns results grouped by document.

## Parameters

- **searchTerm** (string, required): Text to search for
- **caseSensitive** (boolean, required): Whether the search should be case-sensitive
- **wholeWord** (boolean, required): Match whole words only
- **maxResultsPerDocument** (integer, required): Maximum number of results to return per document

## Returns

Returns a JSON object with search results across all documents:
```json
{
  "success": true,
  "searchTerm": "important",
  "documentsSearched": 5,
  "totalResults": 34,
  "results": [
    {
      "filePath": "/path/to/document1.pdf",
      "resultsCount": 12,
      "results": [
        {
          "page": 3,
          "position": 156,
          "context": "...this is an important consideration...",
          "highlightedText": "important"
        }
      ]
    }
  ]
}
```

## Example

```javascript
// Search across all loaded documents
search_all_documents({
  searchTerm: "confidential",
  caseSensitive: false,
  wholeWord: true,
  maxResultsPerDocument: 10
})

// Case-sensitive search
search_all_documents({
  searchTerm: "API",
  caseSensitive: true,
  wholeWord: true,
  maxResultsPerDocument: 20
})
```
