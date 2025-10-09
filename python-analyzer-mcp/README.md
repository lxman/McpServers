# Python Analyzer MCP Server

A Model Context Protocol (MCP) server that exposes Python code analysis capabilities with version awareness. This server can analyze Python code for errors, extract symbols, calculate metrics, format code, and check version compatibility.

## Features

- **Code Analysis**: Detect syntax errors, undefined variables, and compatibility issues
- **Version Awareness**: Automatically detect target Python version or specify manually
- **Symbol Extraction**: List all classes, functions, variables with type annotations
- **Code Formatting**: Format code using black
- **Code Metrics**: Calculate cyclomatic complexity, maintainability index, and line counts
- **Compatibility Checking**: Warn about features not available in target Python version

## Installation

```bash
cd C:\Users\jorda\RiderProjects\McpServers\python-analyzer-mcp
pip install -r requirements.txt
```

## Usage

The server runs over stdio and is designed to be used with MCP clients like Claude Desktop.

### Configuration for Claude Desktop

Add to your Claude Desktop configuration:

```json
{
  "mcpServers": {
    "python-analyzer": {
      "command": "python",
      "args": [
        "-m",
        "src.main"
      ],
      "cwd": "C:\\Users\\jorda\\RiderProjects\\McpServers\\python-analyzer-mcp"
    }
  }
}
```

## Available Tools

### 1. `analyze_code`
Analyze Python code for errors, warnings, and compatibility issues.

**Parameters:**
- `code` (string, required): Python code to analyze
- `fileName` (string, optional): File name for context
- `pythonVersion` (string, optional): Target Python version ("3.8", "3.10", or "auto")

**Example:**
```python
{
  "code": "x: int = 'hello'  # Type mismatch",
  "pythonVersion": "3.10"
}
```

**Response:**
```json
{
  "success": true,
  "detected_version": "3.10",
  "diagnostics": [
    {
      "message": "undefined name 'undefined_var'",
      "category": "PyflakesWarning",
      "severity": "error",
      "line": 2,
      "column": 1
    }
  ],
  "error_count": 1,
  "warning_count": 0
}
```

### 2. `get_symbols`
Extract all symbols (classes, functions, variables) from Python code.

**Parameters:**
- `code` (string, required): Python code to analyze
- `fileName` (string, optional): File name for context
- `filter` (string, optional): Filter by type ('class', 'function', 'variable', 'all')

**Example:**
```python
{
  "code": "class MyClass:\\n    def method(self): pass",
  "filter": "all"
}
```

**Response:**
```json
{
  "success": true,
  "symbols": [
    {
      "name": "MyClass",
      "kind": "class",
      "line": 1,
      "column": 0,
      "decorators": null
    },
    {
      "name": "method",
      "kind": "function",
      "line": 2,
      "column": 4,
      "type_annotation": null,
      "is_async": false
    }
  ],
  "count": 2
}
```

### 3. `format_code`
Format Python code using black formatter.

**Parameters:**
- `code` (string, required): Python code to format

**Example:**
```python
{
  "code": "def test(  ):x=1;return x"
}
```

**Response:**
```json
{
  "success": true,
  "formatted_code": "def test():\\n    x = 1\\n    return x\\n"
}
```

### 4. `calculate_metrics`
Calculate code quality metrics.

**Parameters:**
- `code` (string, required): Python code to analyze
- `fileName` (string, optional): File name for context

**Example:**
```python
{
  "code": "def complex_function(x):\\n    if x > 0:\\n        for i in range(x):\\n            if i % 2 == 0:\\n                print(i)\\n    return x"
}
```

**Response:**
```json
{
  "success": true,
  "metrics": {
    "lines_of_code": 6,
    "comment_lines": 0,
    "blank_lines": 0,
    "total_lines": 6,
    "cyclomatic_complexity": 4,
    "maintainability_index": 65.2,
    "function_count": 1,
    "class_count": 0,
    "average_complexity": 4.0
  }
}
```

## Version Awareness

The analyzer automatically detects the target Python version from:
1. Shebang line (`#!/usr/bin/env python3.10`)
2. Comments (`# requires: python>=3.8`)
3. Syntax features used in the code
4. Explicit `pythonVersion` parameter

### Compatibility Warnings

The tool will warn you if you use features not available in the target version:

```python
# Target: Python 3.7
match value:  # WARNING: match/case requires Python 3.10+
    case 1:
        print("one")
```

## Python Version Features Detected

| Version | Features Detected |
|---------|------------------|
| 3.12 | Type parameter syntax, f-string improvements |
| 3.11 | Exception groups, tomllib |
| 3.10 | match/case, union types with \| |
| 3.9 | Dict merge \|, str methods |
| 3.8 | Walrus operator :=, positional-only parameters |
| 3.7 | dataclasses, postponed annotations |
| 3.6 | f-strings, variable annotations |
| 3.5 | async/await, type hints |

## Dependencies

- **mcp**: Model Context Protocol SDK
- **pylint**: Code analysis
- **pyflakes**: Fast static checking
- **mypy**: Type checking
- **black**: Code formatting
- **radon**: Code metrics
- **jedi**: Type inference

## Development

### Running Locally
```bash
python -m src.main
```

### Testing
```python
# Test analysis
python -c "from src.tools import PythonTools; pt = PythonTools(); print(pt.analyze_code('print(hello)'))"
```

## Architecture

```
python-analyzer-mcp/
├── src/
│   ├── models/          # Data models
│   ├── services/        # PythonAnalyzer service
│   ├── tools/           # MCP tool handlers
│   └── main.py          # MCP server entry point
├── requirements.txt     # Python dependencies
├── pyproject.toml       # Project configuration
└── README.md            # This file
```

## License

MIT

## Token Count
**Remaining: ~50,872 tokens** (out of 190,000)
