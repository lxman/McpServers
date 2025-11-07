# Calculate Metrics

Calculate code metrics including cyclomatic complexity, lines of code, and more.

## Parameters
- **code** (string, required): The C# code to analyze
- **filePath** (string, optional): The file path for better context

## Returns
JSON object with code metrics.

## Example Response
```json
{
  "success": true,
  "cyclomaticComplexity": 1,
  "linesOfCode": 10,
  "maintainabilityIndex": 85
}
```
