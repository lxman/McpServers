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

## Response Size Limits

MCP protocol has a hard limit of **~25,000 tokens** per response. SqlMcp enforces a safe limit of **20,000 tokens** (~80,000 characters) to provide buffer space and helpful error messages.

### Protected Tools

Tools that could return large datasets are protected:
- **execute_query** - Query results with many rows or wide tables
- **list_tables** - Databases with thousands of tables

### Error Response Format

When a response exceeds safe limits, you'll receive:
```json
{
  "success": false,
  "error": "RESPONSE_TOO_LARGE",
  "message": "The query result is too large to return safely...",
  "details": {
    "characterCount": 450000,
    "estimatedTokens": 112500,
    "safeLimit": 20000,
    "hardLimit": 25000,
    "percentageOverLimit": 462
  },
  "explanation": "Query returned 5000 rows with 112,500 estimated tokens...",
  "suggestions": "Try these workarounds:\n  1. Reduce maxRows parameter...",
  "additionalInfo": {
    "rowsReturned": 5000,
    "currentMaxRows": 1000,
    "suggestedMaxRows": 100
  }
}
```

### Workaround Strategies

When you encounter size limit errors:

1. **Reduce Result Size**
   - Decrease `maxRows` parameter (try 100, 50, or 10)
   - Add `WHERE` clause to filter results
   - Select fewer columns (only what you need)
   - Add `TOP`/`LIMIT` clause in SQL

2. **Check Before Fetching**
   - Use `COUNT(*)` to check result size first
   - Use `SELECT TOP 1` to verify query works

3. **Pagination**
   - Use `OFFSET`/`FETCH` or `LIMIT`/`OFFSET` for pagination
   - Process results in smaller batches

4. **Aggregation**
   - Use `GROUP BY` to summarize data
   - Use aggregate functions (`SUM`, `AVG`, `COUNT`)

5. **Schema Queries**
   - Instead of `list_tables`, query system tables directly with filters
   - Example: `SELECT name FROM sys.tables WHERE name LIKE 'prefix%'`
