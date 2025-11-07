# Get Code Fixes

Get code fix suggestions for diagnostics (errors and warnings).

## Parameters
- **code** (string, required): The C# code to analyze
- **filePath** (string, optional): The file path for better context

## Returns
JSON object with code fix suggestions.

## Example Response
```json
{
  "success": true,
  "codeFixes": [],
  "suggestedActions": []
}
```
