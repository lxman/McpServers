# Search Index

Performs a full-text search across an indexed document collection. Returns matching results with context and relevance scores.

## Parameters

- **indexName** (string, required): Name of the index to search
- **query** (string, required): Search query string
- **fuzzy** (boolean, required): Enable fuzzy matching for approximate search
- **maxResults** (integer, required): Maximum number of results to return

## Returns

Returns a JSON object with search results:
```json
{
  "success": true,
  "indexName": "reports_2024",
  "query": "financial report",
  "resultsCount": 15,
  "maxResults": 20,
  "searchTime": 0.045,
  "results": [
    {
      "document": "C:\\Documents\\report1.pdf",
      "page": 5,
      "score": 0.95,
      "context": "...the financial report shows significant growth...",
      "highlightedText": "financial report",
      "position": 234
    }
  ]
}
```

## Example

```javascript
// Basic search
search_index({
  indexName: "reports_2024",
  query: "financial report",
  fuzzy: false,
  maxResults: 20
})

// Fuzzy search for approximate matching
search_index({
  indexName: "technical_docs",
  query: "configuraton",
  fuzzy: true,
  maxResults: 10
})
```
