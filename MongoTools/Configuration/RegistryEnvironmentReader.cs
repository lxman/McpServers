using RegistryTools;

namespace MongoTools.Configuration;

/// <summary>
/// Reads environment variables from the Windows Registry as a fallback
/// when system environment variables are not inherited by the process
/// </summary>
public static class RegistryEnvironmentReader
{
    private const string SystemEnvironmentPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Environment";
    private const string UserEnvironmentPath = @"HKEY_CURRENT_USER\Environment";

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

            // Try user environment first
            if (registry.ValueExists(UserEnvironmentPath, variableName))
            {
                var value = registry.ReadValue(UserEnvironmentPath, variableName);
                if (value != null)
                {
                    return value.ToString();
                }
            }

            // Fall back to system environment
            if (registry.ValueExists(SystemEnvironmentPath, variableName))
            {
                var value = registry.ReadValue(SystemEnvironmentPath, variableName);
                if (value != null)
                {
                    return value.ToString();
                }
            }

            return null;
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
        // First try the normal process environment
        var value = Environment.GetEnvironmentVariable(variableName);
        if (!string.IsNullOrEmpty(value))
        {
            return value;
        }

        // Fall back to registry if not found
        return GetEnvironmentVariable(variableName);
    }

    /// <summary>
    /// Attempts to read MongoDB connection settings from environment variables (with registry fallback)
    /// </summary>
    /// <returns>Tuple of (ConnectionString, Database) if found, or (null, null) if not found</returns>
    public static (string? ConnectionString, string? Database) GetMongoConnectionFromEnvironment()
    {
        var connectionString = GetEnvironmentVariableWithFallback("MONGODB_CONNECTION_STRING");
        var database = GetEnvironmentVariableWithFallback("MONGODB_DATABASE");

        return (connectionString, database);
    }
}
