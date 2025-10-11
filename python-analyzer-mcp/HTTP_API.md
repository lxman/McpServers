# Python Analyzer HTTP API

## Server Information
- **Port**: 7301
- **Base URL**: `http://localhost:7301`

## Starting the Server

```bash
# Windows
run-http-server.bat

# Linux/Mac
python http_server.py
```

## Endpoints

### GET /description
Returns the OpenAPI 3.0 specification.

### POST /api/python/analyze
Analyze Python code for errors and warnings.
- Request: `{"code": "...", "fileName": "...", "pythonVersion": "auto"}`

### POST /api/python/symbols
Extract symbols (classes, functions, variables).
- Request: `{"code": "...", "fileName": "...", "filter": "all"}`
- Filter options: "class", "function", "variable", "all"

### POST /api/python/format
Format code using black.
- Request: `{"code": "..."}`

### POST /api/python/metrics
Calculate code metrics and complexity.
- Request: `{"code": "...", "fileName": "..."}`

### POST /api/python/type-check
Run static type checking using mypy.
- Request: `{"code": "...", "fileName": "..."}`

### POST /api/python/detect-dead-code
Detect unused functions, classes, and variables using vulture.
- Request: `{"code": "...", "fileName": "..."}`

### POST /api/python/lint
Run comprehensive linting using pylint.
- Request: `{"code": "...", "fileName": "..."}`

### POST /api/python/completions
Get code completions using jedi.
- Request: `{"code": "...", "line": 1, "column": 0}`

### POST /api/python/format-autopep8
Format code using autopep8 (alternative to black).
- Request: `{"code": "...", "maxLineLength": 79}`

## Integration with DirectoryMcp

```json
{
  "python-analyzer": {
    "name": "Python Analyzer Tools",
    "url": "http://localhost:7301"
  }
}
```
