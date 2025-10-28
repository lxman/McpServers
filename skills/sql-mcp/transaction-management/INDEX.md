# Transaction Management

Control database transactions for atomic operations.

## Tools

- [begin_transaction](begin_transaction.md) - Start transaction
- [commit_transaction](commit_transaction.md) - Commit changes
- [rollback_transaction](rollback_transaction.md) - Rollback changes
- [list_active_transactions](list_active_transactions.md) - Show active transactions

## Transaction Flow

1. `begin_transaction()` → returns transactionId
2. Execute queries within transaction
3. `commit_transaction(id)` or `rollback_transaction(id)`

## Isolation Levels

- **ReadUncommitted** - Lowest isolation
- **ReadCommitted** - Default
- **RepeatableRead** - Higher consistency
- **Serializable** - Highest isolation

## Best Practices

- Keep transactions short
- Always commit or rollback
- Handle errors with rollback
- Use appropriate isolation level
