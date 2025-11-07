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
- **Response size protection**: If response exceeds 20,000 token limit (~80KB), response is blocked with error (see [../COMMON.md#response-size-limits](../COMMON.md#response-size-limits))
- **Large database workarounds**: Query specific tables by name using get_table_schema, or use SQL to filter tables directly (e.g., `SELECT name FROM sys.tables WHERE name LIKE 'prefix%'`)
