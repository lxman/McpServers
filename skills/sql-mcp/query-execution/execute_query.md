# execute_query

Execute SQL SELECT query and return results.

## Parameters

- **connectionName** (string, required): Connection name
- **sql** (string, required): SELECT statement
- **parameters** (object, optional): Named parameters
- **maxRows** (int, optional): Max rows (default: 1000, max: 10000)

## Returns

```json
{
  "success": true,
  "data": [
    { "id": 1, "name": "Alice" },
    { "id": 2, "name": "Bob" }
  ],
  "rowsAffected": 2,
  "executionTimeMs": 15,
  "isTruncated": false
}
```

## Examples

### Simple Query
```
execute_query("default", "SELECT * FROM Users")
```

### Parameterized Query
```
execute_query(
  "default",
  "SELECT * FROM Users WHERE Age > @age",
  { "age": 21 }
)
```

### Limited Results
```
execute_query("default", "SELECT * FROM LargeTable", null, 100)
```

## Notes

- Use parameters to prevent SQL injection
- Check `isTruncated` for partial results
- Results are dynamic objects (flexible schema)
