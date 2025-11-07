# Get Assembly Info

Get detailed metadata and information about a .NET assembly.

## Parameters
- **assemblyPath** (string, required): The full path to the assembly file

## Returns
JSON object with assembly metadata including version, references, and target framework.

## Example Response
```json
{
  "success": true,
  "assemblyName": "MyAssembly",
  "version": "1.0.0.0",
  "targetFramework": "net9.0",
  "references": []
}
```
