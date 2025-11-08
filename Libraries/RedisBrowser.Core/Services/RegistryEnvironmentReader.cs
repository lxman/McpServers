using RegistryTools;

namespace RedisBrowser.Core.Services;

/// <summary>
/// Reads environment variables from the Windows Registry as a fallback
/// when system environment variables are not inherited by the process
/// </summary>
public static class RegistryEnvironmentReader
{
    private const string SYSTEM_ENVIRONMENT_PATH = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Environment";
    private const string USER_ENVIRONMENT_PATH = @"HKEY_CURRENT_USER\Environment";

    /// <summary>
    /// Attempts to read an environment variable from the Windows Registry
    /// Checks user environment first, then system environment
    /// </summary>
    /// <param name="variableName">Name of the environment variable</param>
    /// <returns>Value of the environment variable, or null if not found</returns>
    public static string? GetEnvironmentVariable(string variableName)
    {
        try
        {
            using var registry = new RegistryManager(RegistryAccessMode.ReadOnly);

            // Try the user environment first
            if (!registry.ValueExists(USER_ENVIRONMENT_PATH, variableName))
                return !registry.ValueExists(SYSTEM_ENVIRONMENT_PATH, variableName)
                    ? null
                    : registry.ReadValue(SYSTEM_ENVIRONMENT_PATH, variableName)?.ToString();
            
            var value = registry.ReadValue(USER_ENVIRONMENT_PATH, variableName);
            if (value != null)
            {
                return value.ToString();
            }

            // Fall back to system environment
            return !registry.ValueExists(SYSTEM_ENVIRONMENT_PATH, variableName)
                ? null
                : registry.ReadValue(SYSTEM_ENVIRONMENT_PATH, variableName)?.ToString();
        }
        catch (Exception)
        {
            // If registry access fails, return null
            return null;
        }
    }

    /// <summary>
    /// Gets an environment variable, first checking the process environment,
    /// then falling back to the registry if not found
    /// </summary>
    /// <param name="variableName">Name of the environment variable</param>
    /// <returns>Value of the environment variable, or null if not found</returns>
    public static string? GetEnvironmentVariableWithFallback(string variableName)
    {
        // First, try the normal process environment
        var value = Environment.GetEnvironmentVariable(variableName);
        return !string.IsNullOrEmpty(value) ? value :
            // Fall back to registry if not found
            GetEnvironmentVariable(variableName);
    }

    /// <summary>
    /// Attempts to read Redis connection string from environment variables (with registry fallback)
    /// </summary>
    /// <returns>Redis connection string if found, or null if not found</returns>
    public static string? GetRedisConnectionFromEnvironment()
    {
        return GetEnvironmentVariableWithFallback("REDIS_CONNECTION_STRING");
    }
}
