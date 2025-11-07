# Analyze Code

Analyze C# code for errors, warnings, and diagnostics using Roslyn.

## Parameters
- **code** (string, required): The C# code to analyze
- **filePath** (string, optional): The file path for better context

## Returns
JSON object with diagnostics, errors, warnings, and analysis results.

## Example Response
```json
{
  "success": true,
  "diagnostics": [],
  "errorCount": 0,
  "warningCount": 0
}
```
