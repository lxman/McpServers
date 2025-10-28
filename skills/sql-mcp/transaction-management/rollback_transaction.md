# rollback_transaction

Rollback a transaction, discarding all changes.

## Parameters

- **transactionId** (string, required): Transaction ID from begin_transaction

## Returns

```json
{
  "success": true,
  "transactionId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "message": "Transaction rolled back"
}
```

## Example

```
var txId = begin_transaction("default")
try {
  execute_non_query("default", "UPDATE Users ...")
  commit_transaction(txId)
} catch {
  rollback_transaction(txId)
}
```

## Notes

- Undoes all changes in transaction
- Releases connection resources
- Use on errors to maintain consistency
- Transaction ID becomes invalid after rollback
