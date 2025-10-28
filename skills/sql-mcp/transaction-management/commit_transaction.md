# commit_transaction

Commit a transaction, persisting all changes.

## Parameters

- **transactionId** (string, required): Transaction ID from begin_transaction

## Returns

```json
{
  "success": true,
  "transactionId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "message": "Transaction committed"
}
```

## Example

```
var txId = begin_transaction("default")
execute_non_query("default", "INSERT INTO Users ...")
commit_transaction(txId)
```

## Notes

- Finalizes all changes in transaction
- Releases connection resources
- Irreversible operation
- Transaction ID becomes invalid after commit
