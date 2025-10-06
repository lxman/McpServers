# Azure SQL MCP Services

Comprehensive SQL capabilities for Azure MCP, supporting Azure SQL Database, PostgreSQL, and MySQL.

## Architecture

The SQL services are organized into two main areas:

### 1. Database Management (`DbManagement/`)
Manages Azure SQL, PostgreSQL, and MySQL resources through Azure Resource Manager.

**Key Features:**
- List and manage SQL servers across subscriptions
- Create, list, and delete databases
- Configure firewall rules
- Manage elastic pools
- Support for Azure SQL, PostgreSQL, and MySQL

**Services:**
- `ISqlDatabaseService` / `SqlDatabaseService`

### 2. Query Execution (`QueryExecution/`)
Executes SQL queries directly against databases with full Azure AD authentication support.

**Key Features:**
- Execute SELECT queries with result sets
- Execute non-query commands (INSERT, UPDATE, DELETE, DDL)
- Transaction support
- Schema introspection
- Azure AD authentication for Azure SQL
- Username/password authentication
- Connection testing

**Services:**
- `ISqlQueryService` / `SqlQueryService`

## MCP Tools Available

### Database Management Tools

#### Server Management
- `azure:list_servers` - List all SQL servers
- `azure:get_server` - Get server details

#### Database Operations
- `azure:list_databases` - List databases on a server
- `azure:get_database` - Get database details
- `azure:create_database` - Create a new database
- `azure:delete_database` - Delete a database

#### Firewall Rules
- `azure:list_firewall_rules` - List firewall rules
- `azure:create_firewall_rule` - Create firewall rule
- `azure:delete_firewall_rule` - Delete firewall rule

#### Elastic Pools
- `azure:list_elastic_pools` - List elastic pools

#### PostgreSQL
- `azure:list_postgre_sql_servers` - List PostgreSQL servers
- `azure:list_postgre_sql_databases` - List PostgreSQL databases

#### MySQL
- `azure:list_my_sql_servers` - List MySQL servers
- `azure:list_my_sql_databases` - List MySQL databases

### Query Execution Tools

- `azure:execute_query` - Execute SELECT queries
- `azure:execute_non_query` - Execute INSERT/UPDATE/DELETE/DDL
- `azure:test_connection` - Test database connectivity
- `azure:get_schema_info` - Get database schema information
- `azure:execute_transaction` - Execute multiple commands in a transaction

## Usage Examples

### 1. List All SQL Servers

```json
{
  "tool": "azure:list_servers",
  "arguments": {
    "subscriptionId": "your-subscription-id",
    "resourceGroupName": "your-resource-group"
  }
}
```

### 2. Create a Database

```json
{
  "tool": "azure:create_database",
  "arguments": {
    "databaseName": "mydb",
    "serverName": "myserver",
    "resourceGroupName": "my-rg",
    "serviceObjective": "Basic",
    "maxSizeBytes": 2147483648
  }
}
```

### 3. Create Firewall Rule

```json
{
  "tool": "azure:create_firewall_rule",
  "arguments": {
    "ruleName": "AllowMyIP",
    "serverName": "myserver",
    "resourceGroupName": "my-rg",
    "startIpAddress": "203.0.113.1",
    "endIpAddress": "203.0.113.1"
  }
}
```

### 4. Execute Query (Azure AD Authentication)

```json
{
  "tool": "azure:execute_query",
  "arguments": {
    "connectionInfoJson": "{\"ServerName\":\"myserver.database.windows.net\",\"DatabaseName\":\"mydb\",\"DatabaseType\":\"AzureSQL\",\"UseAzureAD\":true,\"Port\":1433}",
    "query": "SELECT TOP 10 * FROM Users WHERE Active = 1",
    "maxRows": 1000,
    "timeoutSeconds": 30
  }
}
```

### 5. Execute Query (Username/Password)

```json
{
  "tool": "azure:execute_query",
  "arguments": {
    "connectionInfoJson": "{\"ServerName\":\"myserver.database.windows.net\",\"DatabaseName\":\"mydb\",\"DatabaseType\":\"AzureSQL\",\"UseAzureAD\":false,\"UserName\":\"sqladmin\",\"Password\":\"YourPassword123!\",\"Port\":1433}",
    "query": "SELECT * FROM Products WHERE CategoryId = 5"
  }
}
```

### 6. Execute Non-Query Command

```json
{
  "tool": "azure:execute_non_query",
  "arguments": {
    "connectionInfoJson": "{\"ServerName\":\"myserver.database.windows.net\",\"DatabaseName\":\"mydb\",\"DatabaseType\":\"AzureSQL\",\"UseAzureAD\":true}",
    "command": "UPDATE Users SET LastLogin = GETDATE() WHERE UserId = 123"
  }
}
```

### 7. Get Schema Information

```json
{
  "tool": "azure:get_schema_info",
  "arguments": {
    "connectionInfoJson": "{\"ServerName\":\"myserver.database.windows.net\",\"DatabaseName\":\"mydb\",\"DatabaseType\":\"AzureSQL\",\"UseAzureAD\":true}",
    "tableName": "Users"
  }
}
```

### 8. Execute Transaction

```json
{
  "tool": "azure:execute_transaction",
  "arguments": {
    "connectionInfoJson": "{\"ServerName\":\"myserver.database.windows.net\",\"DatabaseName\":\"mydb\",\"DatabaseType\":\"AzureSQL\",\"UseAzureAD\":true}",
    "commandsJson": "[\"INSERT INTO Orders (CustomerId, OrderDate) VALUES (123, GETDATE())\",\"UPDATE Customers SET LastOrderDate = GETDATE() WHERE CustomerId = 123\"]"
  }
}
```

### 9. PostgreSQL Query

```json
{
  "tool": "azure:execute_query",
  "arguments": {
    "connectionInfoJson": "{\"ServerName\":\"mypostgres.postgres.database.azure.com\",\"DatabaseName\":\"mydb\",\"DatabaseType\":\"PostgreSQL\",\"UserName\":\"myadmin\",\"Password\":\"password\",\"Port\":5432,\"Encrypt\":true}",
    "query": "SELECT * FROM public.users LIMIT 10"
  }
}
```

### 10. MySQL Query

```json
{
  "tool": "azure:execute_query",
  "arguments": {
    "connectionInfoJson": "{\"ServerName\":\"mymysql.mysql.database.azure.com\",\"DatabaseName\":\"mydb\",\"DatabaseType\":\"MySQL\",\"UserName\":\"myadmin\",\"Password\":\"password\",\"Port\":3306,\"Encrypt\":true}",
    "query": "SELECT * FROM users LIMIT 10"
  }
}
```

## Connection Info Format

The `connectionInfoJson` parameter accepts the following properties:

```json
{
  "ServerName": "server.database.windows.net",
  "DatabaseName": "database-name",
  "DatabaseType": "AzureSQL|PostgreSQL|MySQL",
  "Port": 1433,
  "UseAzureAD": true,
  "UserName": "optional-username",
  "Password": "optional-password",
  "IntegratedSecurity": false,
  "ConnectionTimeout": 30,
  "Encrypt": true,
  "TrustServerCertificate": false,
  "AdditionalParameters": {
    "key": "value"
  }
}
```

### Authentication Methods

#### Azure AD Authentication (Recommended for Azure SQL)
```json
{
  "ServerName": "myserver.database.windows.net",
  "DatabaseName": "mydb",
  "DatabaseType": "AzureSQL",
  "UseAzureAD": true
}
```

Uses Azure credential chain (Azure CLI, Visual Studio, Environment Variables, etc.)

#### SQL Authentication
```json
{
  "ServerName": "myserver.database.windows.net",
  "DatabaseName": "mydb",
  "DatabaseType": "AzureSQL",
  "UseAzureAD": false,
  "UserName": "sqladmin",
  "Password": "password"
}
```

#### PostgreSQL Authentication
```json
{
  "ServerName": "mypostgres.postgres.database.azure.com",
  "DatabaseName": "mydb",
  "DatabaseType": "PostgreSQL",
  "UserName": "myadmin@mypostgres",
  "Password": "password",
  "Port": 5432,
  "Encrypt": true
}
```

#### MySQL Authentication
```json
{
  "ServerName": "mymysql.mysql.database.azure.com",
  "DatabaseName": "mydb",
  "DatabaseType": "MySQL",
  "UserName": "myadmin@mymysql",
  "Password": "password",
  "Port": 3306,
  "Encrypt": true
}
```

## Security Best Practices

1. **Use Azure AD Authentication** for Azure SQL when possible
2. **Configure Firewall Rules** properly to restrict access
3. **Use Managed Identities** in production environments
4. **Enable Encryption** in connection strings
5. **Use Read-Only Connections** for SELECT queries when possible
6. **Implement Query Timeouts** to prevent long-running queries
7. **Use Transactions** for multiple related operations
8. **Limit Max Rows** returned in queries to prevent memory issues

## Supported Database Types

- **Azure SQL Database** - Full support for management and query execution
- **Azure PostgreSQL** - Server listing, database listing, query execution
- **Azure MySQL** - Server listing, database listing, query execution

## Dependencies

- `Azure.ResourceManager.Sql` - Azure SQL management
- `Azure.ResourceManager.PostgreSql` - PostgreSQL management
- `Azure.ResourceManager.MySql` - MySQL management
- `Microsoft.Data.SqlClient` - SQL Server connectivity
- `Npgsql` - PostgreSQL connectivity
- `MySqlConnector` - MySQL connectivity

## Error Handling

All operations return detailed error information:

```json
{
  "success": false,
  "error": "Error message",
  "operation": "ExecuteQuery",
  "exceptionType": "SqlException"
}
```

Query results include execution time and status:

```json
{
  "Success": true,
  "Message": "Query executed successfully. Returned 10 rows.",
  "ColumnNames": ["Id", "Name", "Email"],
  "Rows": [...],
  "RowsAffected": 10,
  "ExecutionTime": "00:00:00.1234567",
  "ErrorMessage": null
}
```

## Performance Considerations

- Default query timeout: 30 seconds
- Default max rows: 1000 (adjustable)
- Connection pooling is handled automatically
- Use transactions for bulk operations
- Consider pagination for large result sets

## Future Enhancements

- Database backup/restore operations
- Import/export (BACPAC) support
- Query plan analysis
- Performance monitoring
- Advanced security auditing
- Connection pooling management
- Stored procedure execution helpers
