# SqlMcp Skills Index

Token-efficient documentation for SQL database MCP tools.

## Categories

- [Connection Management](connection-management/INDEX.md) - Database connection lifecycle
- [Query Execution](query-execution/INDEX.md) - Execute SQL queries and commands
- [Schema Inspection](schema-inspection/INDEX.md) - Explore database structure
- [Transaction Management](transaction-management/INDEX.md) - Transaction control

## Configuration

Connections configured in `appsettings.json`:

```json
{
  "SqlConfiguration": {
    "Connections": {
      "default": {
        "Provider": "SqlServer",
        "ConnectionString": "...",
        "ReadOnly": false
      }
    },
    "Security": {
      "AllowDdl": false,
      "MaxResultRows": 10000,
      "AuditAllQueries": true
    }
  }
}
```

## Supported Providers

- **SqlServer** - Microsoft SQL Server (Microsoft.Data.SqlClient)
- **Sqlite** - SQLite (Microsoft.Data.Sqlite)
- **PostgreSql** - Future support via interface extension

## Common Patterns

See [COMMON.md](COMMON.md) for shared concepts.
