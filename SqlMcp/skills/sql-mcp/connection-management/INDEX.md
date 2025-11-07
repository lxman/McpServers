# Connection Management

Manage database connection lifecycle.

## Tools

- [list_connections](list_connections.md) - List available connections
- [test_connection](test_connection.md) - Test connection status
- [close_connection](close_connection.md) - Close active connection

## Connection Lifecycle

1. Connections opened on first use
2. Reused for subsequent operations
3. Explicitly close or auto-close on shutdown

## Configuration

Connections defined in `appsettings.json` under `SqlConfiguration.Connections`.
