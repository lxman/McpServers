# TypeScript Analyzer HTTP API

## Server Information
- **Port**: 7302
- **Base URL**: `http://localhost:7302`

## Starting the Server

```bash
# Windows
run-http-server.bat

# Linux/Mac
npm install && npm run build && node dist/http_server.js
```

## Endpoints

### GET /description
Returns the OpenAPI 3.0 specification.

### POST /api/typescript/analyze
Analyze TypeScript code for errors and diagnostics.
- Request: `{"code": "...", "filePath": "..."}`

### POST /api/typescript/symbols
Extract symbols (classes, interfaces, functions, methods, properties, enums).
- Request: `{"code": "...", "filePath": "...", "filter": "all"}`
- Filter options: "class", "interface", "function", "method", "property", "enum", "all"

### POST /api/typescript/type-info
Get type information at a specific position.
- Request: `{"code": "...", "line": 1, "column": 0, "filePath": "..."}`

### POST /api/typescript/format
Format TypeScript code.
- Request: `{"code": "...", "filePath": "..."}`

### POST /api/typescript/metrics
Calculate code metrics including cyclomatic complexity.
- Request: `{"code": "...", "filePath": "..."}`

### POST /api/typescript/remove-unused-imports
Remove unused import statements.
- Request: `{"code": "...", "filePath": "..."}`

## Integration with DirectoryMcp

```json
{
  "typescript-analyzer": {
    "name": "TypeScript Analyzer Tools",
    "url": "http://localhost:7302"
  }
}
```
