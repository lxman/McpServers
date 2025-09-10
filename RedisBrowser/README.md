# Redis Browser MCP Integration

A Model Context Protocol (MCP) integration for browsing and interacting with Redis databases through Claude.

## Overview

This integration provides a complete set of Redis database tools accessible through the `redis-browser.*` namespace. It allows you to connect to Redis servers, browse keys, manipulate data, and perform administrative tasks.

## Features

### Connection Management
- **redis-browser.connect** - Connect to Redis server with connection string
- **redis-browser.disconnect** - Disconnect from current Redis server
- **redis-browser.status** - Check current connection status
- **redis-browser.select** - Select Redis database (0-15)

### Key Operations
- **redis-browser.get** - Get value of a Redis key
- **redis-browser.set** - Set key to string value (with optional expiry)
- **redis-browser.delete** - Delete a Redis key
- **redis-browser.exists** - Check if key exists
- **redis-browser.keys** - List keys matching a pattern
- **redis-browser.type** - Get data type of a key
- **redis-browser.ttl** - Get time-to-live of a key
- **redis-browser.expire** - Set expiry time on a key

### Server Administration
- **redis-browser.info** - Get Redis server information and statistics
- **redis-browser.flush** - Delete all keys in current database (DANGER!)
- **redis-browser.help** - Show available commands

## Configuration

### Environment Variables
Set these environment variables for automatic connection:
```bash
REDIS_CONNECTION_STRING=localhost:6379
```

### Configuration File (appsettings.json)
```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379"
  },
  "Redis": {
    "AutoConnect": false,
    "DefaultDatabase": 0
  }
}
```

## Connection String Examples

```bash
# Local Redis (default port)
localhost:6379

# Redis with password
localhost:6379,password=mypassword

# Redis with SSL
localhost:6380,ssl=true,password=mypassword

# Redis Cloud/Remote
myredis.example.com:6379,password=secretkey
```

## Usage Examples

### Basic Connection and Browsing
```
redis-browser.connect "localhost:6379"
redis-browser.status
redis-browser.keys "*"
redis-browser.get "mykey"
```

### Working with Data
```
redis-browser.set "user:123" "John Doe"
redis-browser.set "session:abc" "active" 3600
redis-browser.get "user:123"
redis-browser.expire "user:123" 7200
redis-browser.ttl "session:abc"
```

### Database Management
```
redis-browser.select 1
redis-browser.info "memory"
redis-browser.keys "user:*" 50
```

## Security Considerations

- Connection strings with passwords are masked in status output
- Console output is redirected to prevent JSON-RPC corruption
- Use strong authentication when connecting to production Redis instances
- The `redis-browser.flush` command is destructive - use with extreme caution

## Technical Details

This integration uses:
- **StackExchange.Redis** for Redis client operations
- **ModelContextProtocol** for MCP server implementation
- **Microsoft.Extensions.Hosting** for dependency injection and configuration
- **JSON serialization** for all command responses

All Redis operations are performed asynchronously and return structured JSON responses for easy parsing and display.
