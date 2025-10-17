# RedisBrowser - Registry Integration

## Overview

RedisBrowser now integrates with the `RegistryTools` library to provide a **read-only** fallback mechanism for accessing Redis connection settings from Windows Registry environment variables when the service doesn't inherit system environment variables properly.

## Problem Solved

When Claude (or other processes) start the RedisBrowser service, the service may not inherit system environment variables like `REDIS_CONNECTION_STRING`. This integration solves that problem by reading the environment variable directly from the Windows Registry where it is stored by the operating system.

## How It Works

The connection discovery follows this order:

1. **Environment variables with registry fallback** ⭐ **NEW**
   - First checks process environment variables
   - If not found, reads from Windows Registry at:
     - `HKEY_CURRENT_USER\Environment` (user variables)
     - `HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Environment` (system variables)
2. **Configuration file** - Redis connection string from `appsettings.json`

## Registry Locations

The integration reads from these Windows Registry locations:

- **User Environment Variables**: `HKEY_CURRENT_USER\Environment`
- **System Environment Variables**: `HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Environment`

## Supported Redis Environment Variable

The following Redis environment variable is automatically discovered:

- `REDIS_CONNECTION_STRING` - Full Redis connection string (e.g., `localhost:6379,password=mypassword`)

## Security

- **Read-Only Access**: The integration uses `RegistryAccessMode.ReadOnly` exclusively
- **No Write Operations**: RedisBrowser cannot modify registry values
- **Graceful Failures**: If registry access is denied or fails, the system continues to the next connection source
- **No Credential Storage**: This does not store credentials - it only reads Windows environment variables from their registry storage location
- **Password Masking**: Connection strings are masked in logs and status responses

## Usage

No configuration is required! The registry fallback is automatic and transparent. Just ensure your Redis connection string is set as a Windows environment variable:

### Setting Environment Variables (Windows)

**Option 1: System Properties GUI**
1. Right-click "This PC" → Properties → Advanced system settings
2. Click "Environment Variables"
3. Add variable under "User variables" or "System variables":
   - `REDIS_CONNECTION_STRING` = localhost:6379,password=mypassword

**Option 2: PowerShell (Requires Admin for System variables)**
```powershell
# Set user environment variable
[System.Environment]::SetEnvironmentVariable('REDIS_CONNECTION_STRING', 'localhost:6379,password=mypassword', 'User')

# Set system environment variable (requires admin)
[System.Environment]::SetEnvironmentVariable('REDIS_CONNECTION_STRING', 'localhost:6379,password=mypassword', 'Machine')
```

### Connection String Format

Redis connection strings can include various options:

```
# Simple
localhost:6379

# With password
localhost:6379,password=mypassword

# With SSL
localhost:6380,ssl=true,password=mypassword

# Azure Redis Cache
myredis.redis.cache.windows.net:6380,password=accesskey,ssl=true,abortConnect=false
```

## Implementation Details

### RegistryEnvironmentReader Class

Located in `Services/RegistryEnvironmentReader.cs`, this static helper class provides:

- `GetEnvironmentVariable(string variableName)` - Reads from registry only
- `GetEnvironmentVariableWithFallback(string variableName)` - Tries process environment first, then registry
- `GetRedisConnectionFromEnvironment()` - Returns Redis connection string from environment variables

### RedisService Updates

The `RedisService` class has been updated to use registry fallback in:
- `TryAutoConnectAsync()` method (line 30) - Auto-connect on service startup

The method now calls `RegistryEnvironmentReader.GetEnvironmentVariableWithFallback()` instead of `Environment.GetEnvironmentVariable()`.

## Benefits

1. **Resilient to Environment Inheritance Issues** - Works even when services don't inherit environment properly
2. **Zero Configuration** - Automatic fallback, no additional setup needed
3. **Security Focused** - Read-only access, no credential storage
4. **Graceful Degradation** - If registry access fails, continues to configuration file source
5. **Standard Workflow** - Uses same environment variable name as Redis best practices

## Logging

The system logs connection discovery attempts. Check your logs to see which connection source was used:

```
[Information] Attempting auto-connect using environment variables
```

## Troubleshooting

**Problem**: Connection string not being discovered from registry

**Solutions**:
1. Verify environment variable is set in Windows Registry:
   - Open Registry Editor (`regedit`)
   - Navigate to `HKEY_CURRENT_USER\Environment` or `HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Environment`
   - Check that `REDIS_CONNECTION_STRING` exists
2. Ensure the service has read access to the registry keys
3. Restart the service after setting environment variables
4. Check logs for any exceptions during connection discovery

**Problem**: Registry access denied

**Solution**: The system will gracefully continue to configuration file source. Add connection string to `appsettings.json` instead:

```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379,password=mypassword"
  }
}
```

## Example Configuration Fallback Chain

```
1. Check Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING") → Returns null (not inherited)
2. Check Registry at HKEY_CURRENT_USER\Environment\REDIS_CONNECTION_STRING → Found! ✓
3. Use connection string from registry
4. Auto-connect to Redis successfully
```

## Related Files

- `Services/RegistryEnvironmentReader.cs` - Registry reading logic
- `Services/RedisService.cs` - Connection discovery and auto-connect logic
- `RedisBrowser.csproj` - Project reference to RegistryTools
- `../RegistryTools/` - Shared registry access library

## Configuration File Alternative

While environment variables (with registry fallback) take priority, you can still use `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379,password=mypassword"
  }
}
```

Environment variables will override this setting when present.

## Redis Commands

Once connected, you can use all RedisBrowser commands:

```
# Connection management
GET /redis/connect
GET /redis/disconnect
GET /redis/status

# Key operations
GET /redis/get/{key}
POST /redis/set
DELETE /redis/delete/{key}

# Database operations
GET /redis/keys
GET /redis/info
POST /redis/select-database
```

---

**Last Updated**: 2025-10-17  
**RegistryTools Version**: Read-Only Mode  
**Status**: ✅ Production Ready  
**Token Budget**: ~74,488 tokens remaining
