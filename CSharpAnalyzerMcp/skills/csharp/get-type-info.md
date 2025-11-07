# Get Type Info

Get type information at a specific position in C# code.

## Parameters
- **code** (string, required): The C# code to analyze
- **line** (integer, required): The line number (1-based)
- **column** (integer, required): The column number (1-based)
- **filePath** (string, optional): The file path for better context

## Returns
JSON object with type information at the specified position.

## Example Response
```json
{
  "success": true,
  "typeName": "string",
  "namespace": "System"
}
```
