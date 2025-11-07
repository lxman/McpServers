# get_table_indexes

Get indexes defined on a table.

## Parameters

- **connectionName** (string, required): Connection name
- **tableName** (string, required): Table name

## Returns

```json
{
  "success": true,
  "indexes": [
    {
      "indexName": "PK_Users",
      "tableName": "Users",
      "isUnique": true,
      "isPrimaryKey": true,
      "columns": ["Id"]
    },
    {
      "indexName": "IX_Users_Email",
      "tableName": "Users",
      "isUnique": true,
      "isPrimaryKey": false,
      "columns": ["Email"]
    }
  ]
}
```

## Example

```
get_table_indexes("default", "Users")
```

## Use Cases

- Performance analysis
- Query optimization
- Index coverage review
