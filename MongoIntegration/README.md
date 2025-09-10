# MongoDB Integration for Claude - Enhanced UX

This integration provides MongoDB database operations for Claude through the Model Context Protocol (MCP) with a focus on clear, user-friendly experience and excellent error guidance.

## Recent UX Improvements (2025-07-21)

### 🎯 **Crystal Clear Connection Management**
- **Primary vs Additional Connections**: No more confusion about which connection does what
- **Immediate Error Feedback**: Get actionable error messages in seconds, not minutes of troubleshooting
- **Operation Context**: Every operation tells you exactly which server it used
- **Pre-operation Validation**: Operations fail fast with clear guidance if connections aren't ready

### 🚀 **Quick Start Tools**
- **mongodb:get_capabilities** - Understand what each tool does and when to use it
- **mongodb:get_quick_start** - Get step-by-step workflows for common tasks
- **Enhanced Connection Status** - See all your connections and their capabilities at a glance

## Connection Types Made Simple

### 🔹 **Primary Connection** 
Use `mongodb:connect_primary` for your main database work:
- **Purpose**: Daily CRUD operations (insert, query, update, delete)
- **Server Name**: Always "default"
- **Required For**: All basic database operations

### 🔹 **Additional Connections**
Use `mongodb:connect_additional` for advanced multi-database work:
- **Purpose**: Data comparisons, migrations, cross-server queries
- **Server Name**: Your choice (e.g., "production", "staging")
- **Used For**: Advanced multi-server operations

## Quick Start Workflows

### 👥 **First Time User**
```bash
# 1. Connect to your main database
mongodb:connect_primary "mongodb://localhost:27017" "myapp"

# 2. Check your connection
mongodb:get_connection_status

# 3. Start working with data
mongodb:list_collections
mongodb:insert_one "users" '{"name": "John", "email": "john@example.com"}'
```

### 🔄 **Multi-Database User**
```bash
# 1. Connect to your primary database
mongodb:connect_primary "mongodb://localhost:27017" "dev_db"

# 2. Connect to additional server
mongodb:connect_additional "production" "mongodb://prod-server:27017" "prod_db"

# 3. Compare or migrate data
mongodb:compare_servers "default" "production" "users"
```

## Error Messages That Actually Help

### ❌ **Before (Confusing)**
```
insert_one fails silently or with generic errors
```

### ✅ **After (Actionable)**
```json
{
  "success": false,
  "error": "No primary connection established for insert operation",
  "solution": "Run mongodb:connect_primary to establish your main database connection",
  "suggestion": "Primary connection is required for CRUD operations"
}
```

## Available Tools (Organized by Purpose)

### 🔌 **Basic Connection Operations**
- `mongodb:connect_primary` - Establish main connection for CRUD operations
- `mongodb:connect_additional` - Add servers for multi-database features  
- `mongodb:disconnect_primary` - Disconnect from main database
- `mongodb:disconnect_from_server` - Disconnect from specific server
- `mongodb:get_connection_status` - **Enhanced** - Shows ALL connections and capabilities

### 📊 **CRUD Operations** (Require Primary Connection)
- `mongodb:query` - Find documents with filtering and limiting
- `mongodb:insert_one` - Insert a single document  
- `mongodb:insert_many` - Insert multiple documents
- `mongodb:update_one` - Update a single document
- `mongodb:update_many` - Update multiple documents
- `mongodb:delete_one` - Delete a single document
- `mongodb:delete_many` - Delete multiple documents
- `mongodb:count_documents` - Count documents with optional filtering
- `mongodb:aggregate` - Run aggregation pipelines

### 🛠️ **Administration Operations**
- `mongodb:list_collections` - List all collections in database
- `mongodb:create_index` - Create indexes for better performance
- `mongodb:drop_collection` - Delete entire collections (use with caution!)
- `mongodb:execute_command` - Execute raw MongoDB commands

### 🌐 **Multi-Server Operations**
- `mongodb:list_servers` - View all active server connections
- `mongodb:set_default_server` - Change which server is the default
- `mongodb:get_server_status` - Get detailed status for specific server
- `mongodb:ping_server` - Test connectivity and measure response time
- `mongodb:compare_servers` - Compare collections between servers
- `mongodb:sync_collections` - Synchronize data between servers
- `mongodb:cross_server_query` - Query multiple servers simultaneously
- `mongodb:bulk_transfer` - Transfer multiple collections between servers
- `mongodb:health_dashboard` - Comprehensive server health monitoring

### 🎓 **Learning & Help Tools** 
- `mongodb:get_capabilities` - **NEW** - Understand what each tool does
- `mongodb:get_quick_start` - **NEW** - Get workflow examples and common commands  
- `mongodb:list_connection_profiles` - Show saved connection configurations

### 🔄 **Legacy Support** (Use New Names Instead)
- `mongodb:connect` - **LEGACY** - Use `mongodb:connect_primary` instead
- `mongodb:disconnect` - **LEGACY** - Use `mongodb:disconnect_primary` instead

## Configuration

### Environment Variables (Highest Priority)

Create a `.env` file or set environment variables:

```bash
export MONGODB_CONNECTION_STRING="mongodb://localhost:27017"
export MONGODB_DATABASE="myapp"
```

### Configuration File

Update `appsettings.json`:

```json
{
  "MongoDB": {
    "AutoConnect": true,
    "ConnectionString": "mongodb://localhost:27017",
    "DefaultDatabase": "myapp",
    "DefaultServer": "local",
    "ConnectionProfiles": [
      {
        "Name": "local",
        "ConnectionString": "mongodb://localhost:27017",
        "DefaultDatabase": "dev_db",
        "Description": "Local development server",
        "AutoConnect": true
      },
      {
        "Name": "production", 
        "ConnectionString": "mongodb://prod-server:27017",
        "DefaultDatabase": "prod_db",
        "Description": "Production MongoDB Atlas cluster",
        "AutoConnect": false
      }
    ]
  }
}
```

## Usage Examples

### 🎯 **Check What's Connected**
```bash
mongodb:get_connection_status
# Shows primary connection + all additional servers + their capabilities
```

### 🚀 **Get Help Starting**
```bash
mongodb:get_capabilities
# Explains connection types and what each tool does

mongodb:get_quick_start  
# Step-by-step workflows for common tasks
```

### 📋 **Working with Data**
```bash
# Query with filters
mongodb:query "users" '{"age": {"$gte": 18}}' 10

# Insert with validation
mongodb:insert_one "users" '{"name": "John", "age": 30, "email": "john@example.com"}'

# Update multiple documents  
mongodb:update_many "users" '{"status": "inactive"}' '{"$set": {"archived": true}}'

# Create performance index
mongodb:create_index "users" '{"email": 1}' "email_index"
```

### 🔄 **Multi-Server Operations**
```bash
# Compare data between environments
mongodb:compare_servers "default" "production" "users"

# Sync data (dry run first!)
mongodb:sync_collections "staging" "production" "users" "{}" true

# Query across multiple servers
mongodb:cross_server_query '["local", "staging"]' "orders" '{"status": "pending"}'
```

## Error Troubleshooting Guide

### 🔍 **"No primary connection established"**
**Solution**: Run `mongodb:connect_primary` first
**Why**: CRUD operations need a primary connection to work

### 🔍 **"Server 'xyz' is not connected"**  
**Solution**: Use `mongodb:list_servers` to see available servers
**Fix**: Connect to the server using `mongodb:connect_additional`

### 🔍 **JSON parsing errors**
**Solution**: Verify your JSON syntax in filters and documents
**Help**: Use online JSON validators or start with simple `{}` filters

### 🔍 **Connection timeout**
**Check**: Network connectivity and MongoDB server status  
**Tool**: Use `mongodb:ping_server` to test specific connections

## Critical: MCP Console Output Fix

**IMPORTANT**: This integration prevents console output corruption that can break JSON-RPC communication in MCP.

### The Problem
MongoDB.Driver can output warnings/debug messages to stdout, corrupting JSON-RPC communication with errors like:
- `Unexpected token 'w', "warn: Mode"... is not valid JSON`

### The Solution
The `Program.cs` includes critical console output suppression:

```csharp
// Redirect all console output to prevent JSON-RPC corruption
Console.SetOut(TextWriter.Null);
Console.SetError(TextWriter.Null);
```

## Security Best Practices

1. **Never commit connection strings** to version control
2. **Use environment variables** for production deployments  
3. **Limit database permissions** to only what's needed
4. **Use MongoDB Atlas** for cloud deployments with built-in security
5. **Enable authentication** on MongoDB instances

## Development

To build and run:

```bash
dotnet build
dotnet run
```

**MCP Integration Rules**:
- Never write to `Console.Out` or `Console.Error`
- Disable all console logging providers
- Use file logging for diagnostics
- Always test JSON-RPC communication

## Connection Methods (Auto-Connect Priority)

1. **Environment Variables** (Highest Priority)
2. **Connection Profiles** with `AutoConnect: true`
3. **Configuration File** settings
4. **Manual Connection** (Fallback)

For detailed auto-connect status, use `mongodb:get_auto_connect_status`.

---

## Summary of Improvements

✅ **Renamed tools for clarity**: `connect_primary` vs `connect_additional`  
✅ **Enhanced error messages**: Actionable guidance instead of generic failures  
✅ **Operation context**: Every response tells you which server was used  
✅ **Pre-operation validation**: Fast failures with clear next steps  
✅ **Capability discovery**: `get_capabilities` explains what each tool does  
✅ **Quick start guidance**: `get_quick_start` provides workflow examples  
✅ **Comprehensive status**: `get_connection_status` shows all connections  
✅ **Legacy compatibility**: Old function names still work with guidance to upgrade  

**Result**: Time to first successful operation reduced from minutes to seconds! 🚀
