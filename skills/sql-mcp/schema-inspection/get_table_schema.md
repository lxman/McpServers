# get_table_schema

Get detailed column information for a table.

## Parameters

- **connectionName** (string, required): Connection name
- **tableName** (string, required): Table name

## Returns

```json
{
  "success": true,
  "schema": {
    "tableName": "Users",
    "schema": "dbo",
    "columns": [
      {
        "columnName": "Id",
        "dataType": "int",
        "isNullable": false,
        "isPrimaryKey": true,
        "isIdentity": true,
        "maxLength": null,
        "defaultValue": null
      },
      {
        "columnName": "Name",
        "dataType": "nvarchar",
        "isNullable": false,
        "isPrimaryKey": false,
        "isIdentity": false,
        "maxLength": 100,
        "defaultValue": null
      }
    ]
  }
}
```

## Example

```
get_table_schema("default", "Users")
```

## Use Cases

- Understand table structure
- Generate INSERT statements
- Validate data types
- Document database
