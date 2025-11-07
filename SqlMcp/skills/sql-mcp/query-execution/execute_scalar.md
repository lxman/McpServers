# execute_scalar

Execute query returning single value (COUNT, MAX, etc.).

## Parameters

- **connectionName** (string, required): Connection name
- **sql** (string, required): SQL statement
- **parameters** (object, optional): Named parameters

## Returns

```json
{
  "success": true,
  "scalarValue": 42,
  "executionTimeMs": 8
}
```

## Examples

### Count
```
execute_scalar("default", "SELECT COUNT(*) FROM Users")
```

### Max
```
execute_scalar(
  "default",
  "SELECT MAX(Age) FROM Users WHERE Active = @active",
  { "active": true }
)
```

### Exists Check
```
execute_scalar(
  "default",
  "SELECT CASE WHEN EXISTS(SELECT 1 FROM Users WHERE Email = @email) THEN 1 ELSE 0 END",
  { "email": "test@example.com" }
)
```

## Notes

- Returns first column of first row
- Null if no results
- Efficient for aggregate queries
