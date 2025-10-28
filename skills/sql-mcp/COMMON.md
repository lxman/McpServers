# Common Patterns for SqlMcp

## Result Format

All tools return JSON:
```json
{
  "success": true/false,
  "error": "message if failed",
  ...tool-specific data
}
```

## Query Results

```json
{
  "success": true,
  "data": [...],
  "rowsAffected": 100,
  "executionTimeMs": 45,
  "isTruncated": false
}
```

## Parameterized Queries

Always use parameters to prevent SQL injection:
```json
{
  "sql": "SELECT * FROM Users WHERE Id = @id",
  "parameters": { "id": 123 }
}
```

Dapper handles parameterization automatically.

## Security

- **DDL Operations**: Controlled by `AllowDdl` setting
- **Read-Only Connections**: Set `ReadOnly: true` in config
- **Row Limits**: Max rows enforced by `MaxResultRows`
- **Audit Logging**: All queries logged when `AuditAllQueries: true`

## Error Handling

Failed operations return:
```json
{
  "success": false,
  "error": "detailed error message",
  "executionTimeMs": 10
}
```

## Connection Names

Use connection names from `appsettings.json`:
- `"default"` - Primary connection
- `"sqlite-local"` - Local SQLite database
- Custom names as configured
