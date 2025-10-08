# TypeScript Analyzer MCP Server - Usage Examples

## Tool Examples

### 1. analyze_code - Find Type Errors

**Request:**
```json
{
  "name": "analyze_code",
  "arguments": {
    "code": "const x: number = 'hello'; // Type error!",
    "filePath": "example.ts"
  }
}
```

**Response:**
```json
{
  "success": true,
  "diagnostics": [
    {
      "message": "Type 'string' is not assignable to type 'number'.",
      "category": "Error",
      "code": 2322,
      "file": "example.ts",
      "line": 1,
      "column": 7
    }
  ],
  "errorCount": 1,
  "warningCount": 0,
  "infoCount": 0
}
```

### 2. get_symbols - Extract Code Structure

**Request:**
```json
{
  "name": "get_symbols",
  "arguments": {
    "code": "class User { name: string; greet() { return `Hello ${this.name}`; } }",
    "filter": "all"
  }
}
```

**Response:**
```json
{
  "success": true,
  "symbols": [
    {
      "name": "User",
      "kind": "class",
      "type": "any",
      "line": 1,
      "column": 1
    },
    {
      "name": "name",
      "kind": "property",
      "type": "string",
      "line": 1,
      "column": 13,
      "containerName": "User"
    },
    {
      "name": "greet",
      "kind": "method",
      "type": "any",
      "line": 1,
      "column": 27,
      "containerName": "User"
    }
  ],
  "count": 3
}
```

### 3. get_type_info - Inspect Types at Cursor

**Request:**
```json
{
  "name": "get_type_info",
  "arguments": {
    "code": "const users: Array<string> = ['Alice', 'Bob'];",
    "line": 1,
    "column": 7
  }
}
```

**Response:**
```json
{
  "success": true,
  "typeName": "const",
  "typeString": "const users: string[]",
  "symbolKind": "const",
  "documentation": ""
}
```

### 4. format_code - Auto-format Code

**Request:**
```json
{
  "name": "format_code",
  "arguments": {
    "code": "function  test(  ){const x={a:1,b:2};return x;}"
  }
}
```

**Response:**
```json
{
  "success": true,
  "formattedCode": "function test() {\n  const x = { a: 1, b: 2 };\n  return x;\n}"
}
```

### 5. calculate_metrics - Analyze Code Complexity

**Request:**
```json
{
  "name": "calculate_metrics",
  "arguments": {
    "code": "class Calculator {\n  add(a: number, b: number) {\n    if (a < 0 || b < 0) {\n      throw new Error('Negative numbers');\n    }\n    return a + b;\n  }\n}"
  }
}
```

**Response:**
```json
{
  "success": true,
  "metrics": {
    "linesOfCode": 7,
    "commentLines": 0,
    "blankLines": 0,
    "totalLines": 8,
    "cyclomaticComplexity": 3,
    "functionCount": 1,
    "classCount": 1,
    "interfaceCount": 0
  }
}
```

## Common Use Cases

### Debugging Type Errors
Use `analyze_code` to identify type mismatches before running your code.

### Code Review
Use `get_symbols` to quickly understand the structure of unfamiliar code.

### Type Inspection
Use `get_type_info` to understand complex type definitions at specific positions.

### Code Formatting
Use `format_code` to ensure consistent code style across your project.

### Code Quality Analysis
Use `calculate_metrics` to identify overly complex functions that may need refactoring.

## Integration with Claude

Once configured in Claude Desktop, you can ask questions like:

- "Analyze this TypeScript code for errors: `const x: number = 'hello';`"
- "What symbols are in this class? `class User { name: string; greet() {} }`"
- "What's the type at position (1, 7) in `const x: number = 42;`?"
- "Format this code: `function test(){return 1;}`"
- "Calculate metrics for this code: `class MyClass { ... }`"

Claude will automatically use the appropriate tool to answer your questions!
