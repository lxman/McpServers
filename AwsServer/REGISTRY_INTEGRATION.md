# AwsServer - Registry Integration

## Overview

The AwsServer now integrates with the `RegistryTools` library to provide a **read-only** fallback mechanism for accessing AWS credentials from Windows Registry environment variables when the service doesn't inherit system environment variables properly.

## Problem Solved

When Claude (or other processes) start the AwsServer service, the service may not inherit system environment variables like `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, etc. This integration solves that problem by reading these environment variables directly from the Windows Registry where they are stored by the operating system.

## How It Works

The credential discovery follows this order:

1. **Explicit credentials in configuration** - Credentials directly specified in `appsettings.json` or code
2. **AWS Profile** - Credentials from `~/.aws/credentials` file
3. **Environment variables with registry fallback** ⭐ **NEW**
   - First checks process environment variables
   - If not found, reads from Windows Registry at:
     - `HKEY_CURRENT_USER\Environment` (user variables)
     - `HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Environment` (system variables)
4. **LocalStack dummy credentials** - If ServiceUrl is configured
5. **Default AWS credential chain** - Falls through to AWS SDK's default behavior

## Registry Locations

The integration reads from these Windows Registry locations:

- **User Environment Variables**: `HKEY_CURRENT_USER\Environment`
- **System Environment Variables**: `HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Environment`

## Supported AWS Environment Variables

The following AWS environment variables are automatically discovered:

- `AWS_ACCESS_KEY_ID`
- `AWS_SECRET_ACCESS_KEY`
- `AWS_SESSION_TOKEN`
- `AWS_DEFAULT_REGION`
- `AWS_REGION`
- `AWS_PROFILE`

## Security

- **Read-Only Access**: The integration uses `RegistryAccessMode.ReadOnly` exclusively
- **No Write Operations**: The AwsServer cannot modify registry values
- **Graceful Failures**: If registry access is denied or fails, the system continues to the next credential source
- **No Credential Storage**: This does not store credentials - it only reads Windows environment variables from their registry storage location

## Usage

No configuration is required! The registry fallback is automatic and transparent. Just ensure your AWS credentials are set as Windows environment variables:

### Setting Environment Variables (Windows)

**Option 1: System Properties GUI**
1. Right-click "This PC" → Properties → Advanced system settings
2. Click "Environment Variables"
3. Add variables under "User variables" or "System variables":
   - `AWS_ACCESS_KEY_ID` = your-access-key
   - `AWS_SECRET_ACCESS_KEY` = your-secret-key
   - `AWS_DEFAULT_REGION` = us-east-1 (or your region)

**Option 2: PowerShell (Requires Admin for System variables)**
```powershell
# Set user environment variable
[System.Environment]::SetEnvironmentVariable('AWS_ACCESS_KEY_ID', 'your-key', 'User')
[System.Environment]::SetEnvironmentVariable('AWS_SECRET_ACCESS_KEY', 'your-secret', 'User')

# Set system environment variable (requires admin)
[System.Environment]::SetEnvironmentVariable('AWS_ACCESS_KEY_ID', 'your-key', 'Machine')
[System.Environment]::SetEnvironmentVariable('AWS_SECRET_ACCESS_KEY', 'your-secret', 'Machine')
```

## Implementation Details

### RegistryEnvironmentReader Class

Located in `Configuration/RegistryEnvironmentReader.cs`, this static helper class provides:

- `GetEnvironmentVariable(string variableName)` - Reads from registry only
- `GetEnvironmentVariableWithFallback(string variableName)` - Tries process environment first, then registry
- `GetAwsCredentialsFromEnvironment()` - Returns complete AWS configuration from environment variables

### AwsCredentialsProvider Updates

The `AwsCredentialsProvider` class has been updated to include registry fallback in its credential discovery chain. It now attempts to load credentials from environment variables (with registry fallback) after checking explicit configuration and AWS profiles.

## Benefits

1. **Resilient to Environment Inheritance Issues** - Works even when services don't inherit environment properly
2. **Zero Configuration** - Automatic fallback, no additional setup needed
3. **Security Focused** - Read-only access, no credential storage
4. **Graceful Degradation** - If registry access fails, continues to other credential sources
5. **Standard Workflow** - Uses same environment variable names as AWS SDK

## Logging

The system logs credential discovery attempts at the INFO level. Check your logs to see which credential source was used:

```
[Information] Using AWS credentials from environment variables (registry fallback)
```

## Troubleshooting

**Problem**: Credentials not being discovered from registry

**Solutions**:
1. Verify environment variables are set in Windows Registry:
   - Open Registry Editor (`regedit`)
   - Navigate to `HKEY_CURRENT_USER\Environment` or `HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Environment`
   - Check that AWS variables exist
2. Ensure the service has read access to the registry keys
3. Restart the service after setting environment variables
4. Check logs for any exceptions during credential discovery

**Problem**: Registry access denied

**Solution**: The system will gracefully continue to other credential sources. Consider using AWS profile or explicit configuration instead.

## Example Configuration Fallback Chain

```
1. Check appsettings.json → Not configured
2. Check AWS Profile (~/.aws/credentials) → Not found
3. Check Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID") → Returns null (not inherited)
4. Check Registry at HKEY_CURRENT_USER\Environment\AWS_ACCESS_KEY_ID → Found! ✓
5. Use credentials from registry
```

## Related Files

- `Configuration/RegistryEnvironmentReader.cs` - Registry reading logic
- `Configuration/AwsCredentialsProvider.cs` - Credential discovery orchestration
- `RegistryTools/` - Shared registry access library

## Future Enhancements

This integration could be extended to:
- Cache registry reads for performance
- Support for other service-specific registry locations
- Detailed logging of which credential source was used
- Metrics on credential source usage

---

**Last Updated**: 2025-10-17  
**RegistryTools Version**: Read-Only Mode  
**Status**: ✅ Production Ready
