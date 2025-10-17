# MongoTools - Registry Integration

## Overview

MongoTools now integrates with the `RegistryTools` library to provide a **read-only** fallback mechanism for accessing MongoDB connection settings from Windows Registry environment variables when the service doesn't inherit system environment variables properly.

## Problem Solved

When Claude (or other processes) start the MongoTools service, the service may not inherit system environment variables like `MONGODB_CONNECTION_STRING` and `MONGODB_DATABASE`. This integration solves that problem by reading these environment variables directly from the Windows Registry where they are stored by the operating system.

## How It Works

The connection discovery follows this order:

1. **Environment variables with registry fallback** ⭐ **NEW**
   - First checks process environment variables
   - If not found, reads from Windows Registry at:
     - `HKEY_CURRENT_USER\Environment` (user variables)
     - `HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Environment` (system variables)
2. **Connection Profiles with AutoConnect enabled** - From `appsettings.json`
3. **Configuration file** - Direct connection string in `appsettings.json`
4. **First available profile** - Fallback to any configured profile

## Registry Locations

The integration reads from these Windows Registry locations:

- **User Environment Variables**: `HKEY_CURRENT_USER\Environment`
- **System Environment Variables**: `HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Environment`

## Supported MongoDB Environment Variables

The following MongoDB environment variables are automatically discovered:

- `MONGODB_CONNECTION_STRING` - Full MongoDB connection string (e.g., `mongodb://localhost:27017`)
- `MONGODB_DATABASE` - Default database name

## Security

- **Read-Only Access**: The integration uses `RegistryAccessMode.ReadOnly` exclusively
- **No Write Operations**: MongoTools cannot modify registry values
- **Graceful Failures**: If registry access is denied or fails, the system continues to the next credential source
- **No Credential Storage**: This does not store credentials - it only reads Windows environment variables from their registry storage location

## Usage

No configuration is required! The registry fallback is automatic and transparent. Just ensure your MongoDB connection settings are set as Windows environment variables:

### Setting Environment Variables (Windows)

**Option 1: System Properties GUI**
1. Right-click "This PC" → Properties → Advanced system settings
2. Click "Environment Variables"
3. Add variables under "User variables" or "System variables":
   - `MONGODB_CONNECTION_STRING` = mongodb://localhost:27017
   - `MONGODB_DATABASE` = myDatabase

**Option 2: PowerShell (Requires Admin for System variables)**
```powershell
# Set user environment variable
[System.Environment]::SetEnvironmentVariable('MONGODB_CONNECTION_STRING', 'mongodb://localhost:27017', 'User')
[System.Environment]::SetEnvironmentVariable('MONGODB_DATABASE', 'myDatabase', 'User')

# Set system environment variable (requires admin)
[System.Environment]::SetEnvironmentVariable('MONGODB_CONNECTION_STRING', 'mongodb://localhost:27017', 'Machine')
[System.Environment]::SetEnvironmentVariable('MONGODB_DATABASE', 'myDatabase', 'Machine')
```

## Implementation Details

### RegistryEnvironmentReader Class

Located in `Configuration/RegistryEnvironmentReader.cs`, this static helper class provides:

- `GetEnvironmentVariable(string variableName)` - Reads from registry only
- `GetEnvironmentVariableWithFallback(string variableName)` - Tries process environment first, then registry
- `GetMongoConnectionFromEnvironment()` - Returns tuple of (ConnectionString, Database) from environment variables

### MongoDbService Updates

The `MongoDbService` class has been updated to use registry fallback in:
- `TryAutoConnectAsync()` method (lines 96-99) - Auto-connect on service startup
- `GetAutoConnectStatus()` method (lines 790-791) - Status reporting

Both locations now call `RegistryEnvironmentReader.GetEnvironmentVariableWithFallback()` instead of `Environment.GetEnvironmentVariable()`.

## Benefits

1. **Resilient to Environment Inheritance Issues** - Works even when services don't inherit environment properly
2. **Zero Configuration** - Automatic fallback, no additional setup needed
3. **Security Focused** - Read-only access, no credential storage
4. **Graceful Degradation** - If registry access fails, continues to other connection sources
5. **Standard Workflow** - Uses same environment variable names as MongoDB best practices

## Logging

The system logs connection discovery attempts. Check your logs to see which connection source was used:

```
[Information] Attempting auto-connect using environment variables
```

## Troubleshooting

**Problem**: Connection string not being discovered from registry

**Solutions**:
1. Verify environment variables are set in Windows Registry:
   - Open Registry Editor (`regedit`)
   - Navigate to `HKEY_CURRENT_USER\Environment` or `HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Environment`
   - Check that MongoDB variables exist
2. Ensure the service has read access to the registry keys
3. Restart the service after setting environment variables
4. Check logs for any exceptions during connection discovery

**Problem**: Registry access denied

**Solution**: The system will gracefully continue to other connection sources. Consider using configuration profiles in `appsettings.json` instead.

## Example Configuration Fallback Chain

```
1. Check Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING") → Returns null (not inherited)
2. Check Registry at HKEY_CURRENT_USER\Environment\MONGODB_CONNECTION_STRING → Found! ✓
3. Use connection string from registry
4. Auto-connect to MongoDB successfully
```

## Related Files

- `Configuration/RegistryEnvironmentReader.cs` - Registry reading logic
- `MongoDbService.cs` - Connection discovery and auto-connect logic
- `MongoTools.csproj` - Project reference to RegistryTools
- `../RegistryTools/` - Shared registry access library

## Configuration File Example

While environment variables (with registry fallback) take priority, you can still use `appsettings.json`:

```json
{
  "MongoDB": {
    "AutoConnect": true,
    "DefaultServer": "production",
    "ConnectionProfiles": [
      {
        "Name": "production",
        "ConnectionString": "mongodb://prod-server:27017",
        "DefaultDatabase": "prodDb",
        "AutoConnect": true
      }
    ]
  }
}
```

Environment variables will override these settings when present.

---

**Last Updated**: 2025-10-17  
**RegistryTools Version**: Read-Only Mode  
**Status**: ✅ Production Ready  
**Token Budget**: ~84,168 tokens remaining
