# Flush Database

**⚠️ DESTRUCTIVE OPERATION - USE WITH EXTREME CAUTION ⚠️**

Deletes ALL keys in the currently selected database. This operation is immediate and CANNOT be undone.

## Parameters

This tool takes no parameters.

## Returns

Returns a JSON object confirming the operation:

```json
{
  "success": true,
  "message": "All keys in the current database have been deleted",
  "database": 0,
  "keysDeleted": "all"
}
```

If the operation fails:

```json
{
  "success": false,
  "error": "Failed to flush database: Permission denied"
}
```

## Example

Flush the currently selected database:
```
(no parameters required)
```

## ⚠️ CRITICAL WARNINGS

1. **DATA LOSS**: This command permanently deletes ALL keys in the current database. There is NO way to recover the data after this operation.

2. **IMMEDIATE EXECUTION**: The deletion happens instantly. There is no confirmation prompt or grace period.

3. **DATABASE SCOPE**: This only affects the currently selected database (see `select_database` tool). Other databases remain unaffected.

4. **PRODUCTION RISK**: NEVER use this command on production databases unless you are absolutely certain and have backups.

5. **ALTERNATIVE**: If you want to delete all keys across ALL databases, use the FLUSHALL command (if implemented), but this is even more dangerous.

## Safe Usage Guidelines

Before running this command:
- ✓ Verify you're connected to the correct Redis server
- ✓ Verify you have the correct database selected
- ✓ Ensure you have recent backups if needed
- ✓ Confirm this is not a production environment (unless intentional)
- ✓ Double-check with team members if applicable

Common legitimate use cases:
- Clearing test databases between test runs
- Resetting development environments
- Removing all cached data during deployments
- Cleaning up staging environments

## Example Workflow

```
1. Check connection: get_connection_status
2. Verify correct database: (check selectedDatabase in response)
3. Optionally switch database: select_database with databaseNumber: 5
4. Execute flush: flush_database
```

Remember: With great power comes great responsibility. Use this command wisely.
