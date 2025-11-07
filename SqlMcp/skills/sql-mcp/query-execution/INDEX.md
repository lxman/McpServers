# Query Execution

Execute SQL queries and commands against databases.

## Tools

- [execute_query](execute_query.md) - Run SELECT queries
- [execute_non_query](execute_non_query.md) - Run INSERT/UPDATE/DELETE
- [execute_scalar](execute_scalar.md) - Get single value

## Security

- All queries use Dapper parameterization (SQL injection safe)
- DDL blocked unless `AllowDdl: true`
- Results limited by `MaxResultRows`
- All queries audited when enabled

## Best Practices

- Always use parameters for user input
- Use appropriate tool for operation type
- Handle truncated results (check `isTruncated`)
