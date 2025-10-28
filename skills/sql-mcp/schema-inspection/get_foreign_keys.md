# get_foreign_keys

Get foreign key constraints for a table.

## Parameters

- **connectionName** (string, required): Connection name
- **tableName** (string, required): Table name

## Returns

```json
{
  "success": true,
  "foreignKeys": [
    {
      "constraintName": "FK_Orders_Users",
      "tableName": "Orders",
      "columnName": "UserId",
      "referencedTableName": "Users",
      "referencedColumnName": "Id",
      "deleteRule": "CASCADE",
      "updateRule": "NO_ACTION"
    }
  ]
}
```

## Example

```
get_foreign_keys("default", "Orders")
```

## Use Cases

- Understand relationships
- Document data model
- Plan cascading deletes
- Generate ER diagrams
