# Find Dead Code

Find dead code including unreachable code and unused private members.

## Parameters
- **code** (string, required): The C# code to analyze
- **filePath** (string, optional): The file path for better context

## Returns
JSON object with dead code locations.

## Example Response
```json
{
  "success": true,
  "deadCode": [],
  "unreachableCode": [],
  "unusedMembers": []
}
```
