# list_active_transactions

List all active (uncommitted) transactions.

## Parameters

None

## Returns

```json
{
  "success": true,
  "transactions": [
    "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "b2c3d4e5-f6a7-8901-bcde-f12345678901"
  ]
}
```

## Example

```
list_active_transactions()
```

## Use Cases

- Monitor long-running transactions
- Debug transaction leaks
- Verify transaction cleanup
- System health monitoring

## Notes

- Returns transaction IDs
- Empty array if no active transactions
- Uncommitted transactions hold resources
