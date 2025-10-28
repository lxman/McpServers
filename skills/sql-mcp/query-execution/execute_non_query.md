# execute_non_query

Execute INSERT, UPDATE, or DELETE statements.

## Parameters

- **connectionName** (string, required): Connection name
- **sql** (string, required): SQL statement
- **parameters** (object, optional): Named parameters

## Returns

```json
{
  "success": true,
  "rowsAffected": 3,
  "executionTimeMs": 25
}
```

## Examples

### Insert
```
execute_non_query(
  "default",
  "INSERT INTO Users (Name, Age) VALUES (@name, @age)",
  { "name": "Charlie", "age": 30 }
)
```

### Update
```
execute_non_query(
  "default",
  "UPDATE Users SET Age = @age WHERE Id = @id",
  { "age": 31, "id": 5 }
)
```

### Delete
```
execute_non_query(
  "default",
  "DELETE FROM Users WHERE Age < @minAge",
  { "minAge": 18 }
)
```

## Notes

- Returns number of affected rows
- Use in transactions for atomicity
- Always parameterize user input
