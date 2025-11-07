# List Types

List all types (classes, interfaces, enums, structs) in a .NET assembly.

## Parameters
- **assemblyPath** (string, required): The full path to the assembly file
- **publicOnly** (boolean, optional): Only list public types (default: false)
- **namespaceFilter** (string, optional): Filter by namespace
- **typeKindFilter** (string, optional): Filter by type kind (class, interface, enum, struct)

## Returns
JSON object with list of types and their details.

## Example Response
```json
{
  "success": true,
  "types": [],
  "totalCount": 0
}
```
