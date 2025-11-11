using RegistryTools;

namespace Mcp.Common.Core.Environment;

/// <summary>
/// Provides environment variable access with Windows Registry fallback support.
/// Consolidates environment variable reading logic across all MCP servers.
/// </summary>
public static class EnvironmentReader
{
    private const string SystemEnvironmentPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Environment";
    private const string UserEnvironmentPath = @"HKEY_CURRENT_USER\Environment";

    /// <summary>
    /// Gets environment variable from process environment first, then falls back to Windows Registry.
    /// This is the recommended method for most use cases.
    /// </summary>
    /// <param name="variableName">The name of the environment variable to retrieve</param>
    /// <returns>The environment variable value, or null if not found</returns>
    /// <example>
    /// string? connectionString = EnvironmentReader.GetEnvironmentVariableWithFallback("DATABASE_CONNECTION_STRING");
    /// </example>
    public static string? GetEnvironmentVariableWithFallback(string variableName)
    {
        if (string.IsNullOrWhiteSpace(variableName))
            return null;

        // First try the normal process environment (fastest)
        string? value = System.Environment.GetEnvironmentVariable(variableName);
        if (!string.IsNullOrEmpty(value))
            return value;

        // Fall back to registry if not found in process environment
        return GetEnvironmentVariableFromRegistry(variableName);
    }

    /// <summary>
    /// Gets environment variable directly from Windows Registry.
    /// Searches user environment first, then system environment.
    /// </summary>
    /// <param name="variableName">The name of the environment variable to retrieve</param>
    /// <returns>The environment variable value from registry, or null if not found</returns>
    /// <remarks>
    /// This method bypasses the process environment and reads directly from the Windows Registry.
    /// Use this when you need to access environment variables that may have been set after the process started.
    /// </remarks>
    public static string? GetEnvironmentVariableFromRegistry(string variableName)
    {
        if (string.IsNullOrWhiteSpace(variableName))
            return null;

        try
        {
            using var registry = new RegistryManager(RegistryAccessMode.ReadOnly);

            // Try user environment first (HKEY_CURRENT_USER)
            if (registry.ValueExists(UserEnvironmentPath, variableName))
            {
                object? value = registry.ReadValue(UserEnvironmentPath, variableName);
                if (value != null)
                    return value.ToString();
            }

            // Fall back to system environment (HKEY_LOCAL_MACHINE)
            if (registry.ValueExists(SystemEnvironmentPath, variableName))
            {
                object? value = registry.ReadValue(SystemEnvironmentPath, variableName);
                if (value != null)
                    return value.ToString();
            }

            return null;
        }
        catch (Exception)
        {
            // Silently return null if registry access fails (e.g., permissions, registry key doesn't exist)
            return null;
        }
    }

    /// <summary>
    /// Gets multiple environment variables with fallback support.
    /// </summary>
    /// <param name="variableNames">Array of environment variable names to retrieve</param>
    /// <returns>Dictionary of variable names to their values (excludes variables not found)</returns>
    /// <example>
    /// var vars = EnvironmentReader.GetMultipleEnvironmentVariables("DB_HOST", "DB_PORT", "DB_NAME");
    /// if (vars.TryGetValue("DB_HOST", out string? host)) { ... }
    /// </example>
    public static Dictionary<string, string> GetMultipleEnvironmentVariables(params string[] variableNames)
    {
        var result = new Dictionary<string, string>();

        foreach (string variableName in variableNames)
        {
            string? value = GetEnvironmentVariableWithFallback(variableName);
            if (!string.IsNullOrEmpty(value))
            {
                result[variableName] = value;
            }
        }

        return result;
    }

    /// <summary>
    /// Checks if an environment variable exists (in process environment or registry).
    /// </summary>
    /// <param name="variableName">The name of the environment variable to check</param>
    /// <returns>True if the variable exists and has a non-empty value, false otherwise</returns>
    public static bool EnvironmentVariableExists(string variableName)
    {
        return !string.IsNullOrEmpty(GetEnvironmentVariableWithFallback(variableName));
    }
}
