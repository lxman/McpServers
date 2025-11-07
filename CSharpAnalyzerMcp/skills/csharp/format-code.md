# Format Code

Format C# code using Roslyn formatting rules.

## Parameters
- **code** (string, required): The C# code to format
- **filePath** (string, optional): The file path for better context

## Returns
JSON object with formatted code.

## Example Response
```json
{
  "success": true,
  "formattedCode": "// formatted code here"
}
```
