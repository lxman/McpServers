# Get Symbols

Get all symbols (classes, methods, properties, etc.) from C# code.

## Parameters
- **code** (string, required): The C# code to analyze
- **filePath** (string, optional): The file path for better context
- **filter** (string, optional): Filter symbols by type (e.g., "class", "method", "property")

## Returns
JSON object with symbols and their details.

## Example Response
```json
{
  "success": true,
  "symbols": [],
  "totalCount": 0
}
```
