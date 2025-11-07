# Remove Unused Usings

Remove unused using directives from C# code.

## Parameters
- **code** (string, required): The C# code to clean
- **filePath** (string, optional): The file path for better context

## Returns
JSON object with cleaned code.

## Example Response
```json
{
  "success": true,
  "cleanedCode": "// code with unused usings removed",
  "removedCount": 3
}
```
