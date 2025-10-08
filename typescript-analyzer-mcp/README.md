# TypeScript Analyzer MCP Server

A Model Context Protocol (MCP) server that exposes TypeScript compiler API capabilities for code analysis, type checking, symbol extraction, and more.

## Features

- **Code Analysis**: Get diagnostics, errors, and warnings from TypeScript code
- **Symbol Extraction**: List all classes, interfaces, functions, methods, properties, and enums
- **Type Information**: Get type information at any position in the code
- **Code Formatting**: Format TypeScript code using TypeScript's built-in formatter
- **Code Metrics**: Calculate cyclomatic complexity, lines of code, and other metrics

## Installation

```bash
npm install
npm run build
```

## Usage

The server runs over stdio and is designed to be used with MCP clients like Claude Desktop.

### Configuration for Claude Desktop

Add to your Claude Desktop configuration:

```json
{
  "mcpServers": {
    "typescript-analyzer": {
      "command": "node",
      "args": [
        "C:\\Users\\jorda\\RiderProjects\\McpServers\\typescript-analyzer-mcp\\dist\\index.js"
      ]
    }
  }
}
```

## Available Tools

### 1. `analyze_code`
Analyze TypeScript code for errors, warnings, and diagnostics.

**Parameters:**
- `code` (string, required): TypeScript code to analyze
- `filePath` (string, optional): File path for context

**Example:**
```typescript
{
  "code": "const x: number = 'hello';",
  "filePath": "test.ts"
}
```

### 2. `get_symbols`
Extract all symbols (classes, interfaces, functions, etc.) from TypeScript code.

**Parameters:**
- `code` (string, required): TypeScript code to analyze
- `filePath` (string, optional): File path for context
- `filter` (string, optional): Filter by symbol type ('class', 'interface', 'function', 'method', 'property', 'enum', 'all')

**Example:**
```typescript
{
  "code": "class MyClass { method() {} }",
  "filter": "class"
}
```

### 3. `get_type_info`
Get type information at a specific position in the code.

**Parameters:**
- `code` (string, required): TypeScript code to analyze
- `line` (number, required): Line number (1-based)
- `column` (number, required): Column number (1-based)
- `filePath` (string, optional): File path for context

**Example:**
```typescript
{
  "code": "const x: number = 42;",
  "line": 1,
  "column": 7
}
```

### 4. `format_code`
Format TypeScript code using TypeScript's formatter.

**Parameters:**
- `code` (string, required): TypeScript code to format
- `filePath` (string, optional): File path for context

**Example:**
```typescript
{
  "code": "const x={a:1,b:2};"
}
```

### 5. `calculate_metrics`
Calculate code metrics including complexity and line counts.

**Parameters:**
- `code` (string, required): TypeScript code to analyze
- `filePath` (string, optional): File path for context

**Example:**
```typescript
{
  "code": "class MyClass { /* ... */ }"
}
```

## Response Formats

All tools return JSON responses with a `success` boolean and relevant data or error information.

### Analyze Code Response
```typescript
{
  "success": true,
  "diagnostics": [
    {
      "message": "Type 'string' is not assignable to type 'number'.",
      "category": "Error",
      "code": 2322,
      "line": 1,
      "column": 7
    }
  ],
  "errorCount": 1,
  "warningCount": 0,
  "infoCount": 0
}
```

### Get Symbols Response
```typescript
{
  "success": true,
  "symbols": [
    {
      "name": "MyClass",
      "kind": "class",
      "type": "any",
      "line": 1,
      "column": 1
    }
  ],
  "count": 1
}
```

### Get Type Info Response
```typescript
{
  "success": true,
  "typeName": "const",
  "typeString": "const x: number",
  "symbolKind": "const",
  "documentation": ""
}
```

### Format Code Response
```typescript
{
  "success": true,
  "formattedCode": "const x = { a: 1, b: 2 };"
}
```

### Calculate Metrics Response
```typescript
{
  "success": true,
  "metrics": {
    "linesOfCode": 10,
    "commentLines": 2,
    "blankLines": 1,
    "totalLines": 13,
    "cyclomaticComplexity": 3,
    "functionCount": 2,
    "classCount": 1,
    "interfaceCount": 0
  }
}
```

## Development

### Build
```bash
npm run build
```

### Watch Mode
```bash
npm run watch
```

### Clean
```bash
npm run clean
```

## Architecture

- **TypeScriptAnalyzer**: Core service that wraps the TypeScript compiler API
- **TypeScriptTools**: Tool handlers that process MCP requests
- **Models**: TypeScript interfaces for requests and responses
- **index.ts**: MCP server entry point with tool registration

## License

MIT
