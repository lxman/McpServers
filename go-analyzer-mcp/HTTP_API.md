# Go Analyzer HTTP API

## Server Information
- **Port**: 7300
- **Base URL**: `http://localhost:7300`

## Starting the Server

```bash
# Windows
run-http-server.bat

# Linux/Mac
go run http_server.go
```

## Endpoints

### GET /description
Returns the OpenAPI 3.0 specification for this API.

**Response**: OpenAPI JSON specification

---

### POST /api/go/analyze
Analyze Go code for errors and warnings using `go vet`.

**Request Body**:
```json
{
  "code": "package main\n\nfunc main() { ... }",
  "fileName": "optional_filename.go"
}
```

**Response**:
```json
{
  "success": true,
  "diagnostics": [...],
  "errorCount": 0,
  "warningCount": 0
}
```

---

### POST /api/go/format
Format Go code using `gofmt`.

**Request Body**:
```json
{
  "code": "package main\n\nfunc main(){fmt.Println(\"Hello\")}"
}
```

**Response**:
```json
{
  "success": true,
  "formattedCode": "package main\n\nfunc main() {\n\tfmt.Println(\"Hello\")\n}\n"
}
```

---

### POST /api/go/symbols
Extract symbols (functions, types, variables) from Go code.

**Request Body**:
```json
{
  "code": "package main...",
  "filter": "all"  // Options: "function", "type", "variable", "all"
}
```

**Response**:
```json
{
  "success": true,
  "count": 5,
  "symbols": [
    {
      "name": "main",
      "kind": "function",
      "line": 3,
      "signature": "func main()"
    }
  ]
}
```

---

### POST /api/go/metrics
Calculate code metrics including cyclomatic complexity.

**Request Body**:
```json
{
  "code": "package main..."
}
```

**Response**:
```json
{
  "success": true,
  "metrics": {
    "linesOfCode": 50,
    "commentLines": 10,
    "blankLines": 5,
    "functionCount": 3,
    "typeCount": 2,
    "averageComplexity": 2.5,
    "maxComplexity": 5
  },
  "functionMetrics": [...]
}
```

## Error Handling

All endpoints return errors in the following format:
```json
{
  "success": false,
  "error": "Error message here"
}
```

## Integration with DirectoryMcp

Add to DirectoryMcp configuration:
```json
{
  "go-analyzer": {
    "name": "Go Analyzer Tools",
    "url": "http://localhost:7300"
  }
}
```
