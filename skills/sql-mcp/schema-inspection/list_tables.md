# list_tables

List all tables and views in database.

## Parameters

- **connectionName** (string, required): Connection name

## Returns

```json
{
  "success": true,
  "tables": [
    {
      "tableName": "Users",
      "schema": "dbo",
      "tableType": "BASE TABLE",
      "rowCount": null
    },
    {
      "tableName": "Orders",
      "schema": "dbo",
      "tableType": "BASE TABLE",
      "rowCount": null
    }
  ]
}
```

## Example

```
list_tables("default")
```

## Notes

- Excludes system tables (sqlite_*, INFORMATION_SCHEMA)
- Schema field null for SQLite
- Use to discover database structure
