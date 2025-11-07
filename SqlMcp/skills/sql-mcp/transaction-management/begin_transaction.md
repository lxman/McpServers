# begin_transaction

Start a database transaction.

## Parameters

- **connectionName** (string, required): Connection name
- **isolationLevel** (string, optional): ReadUncommitted, ReadCommitted, RepeatableRead, Serializable

## Returns

```json
{
  "success": true,
  "transactionId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
}
```

## Example

### Default Isolation
```
begin_transaction("default")
```

### Explicit Isolation
```
begin_transaction("default", "Serializable")
```

## Notes

- Returns unique transaction ID
- Use ID for commit/rollback
- Connection held until commit/rollback
- Multiple concurrent transactions supported
