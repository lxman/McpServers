using RegistryTools;

namespace AwsServer.Configuration;

/// <summary>
/// Reads environment variables from the Windows Registry as a fallback
/// when the process does not inherit system environment variables
/// </summary>
public static class RegistryEnvironmentReader
{
    private const string SYSTEM_ENVIRONMENT_PATH = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Environment";
    private const string USER_ENVIRONMENT_PATH = @"HKEY_CURRENT_USER\Environment";

    /// <summary>
    /// Attempts to read an environment variable from the Windows Registry
    /// This checks the user environment first, then system environment
    /// </summary>
    /// <param name="variableName">Name of the environment variable</param>
    /// <returns>Value of the environment variable, or null if not found</returns>
    public static string? GetEnvironmentVariable(string variableName)
    {
        try
        {
            using var registry = new RegistryManager(RegistryAccessMode.ReadOnly);

            // Try the user environment first
            if (registry.ValueExists(USER_ENVIRONMENT_PATH, variableName))
            {
                object? value = registry.ReadValue(USER_ENVIRONMENT_PATH, variableName);
                if (value != null)
                {
                    return value.ToString();
                }
            }

            // Fall back to system environment
            if (!registry.ValueExists(SYSTEM_ENVIRONMENT_PATH, variableName)) return null;
            object? regValue = registry.ReadValue(SYSTEM_ENVIRONMENT_PATH, variableName);
            return regValue?.ToString();
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
        string? value = Environment.GetEnvironmentVariable(variableName);
        return !string.IsNullOrEmpty(value) ? value :
            // Fall back to registry if not found
            GetEnvironmentVariable(variableName);
    }

    /// <summary>
    /// Attempts to read AWS credentials from environment variables (with registry fallback)
    /// </summary>
    /// <returns>AWS configuration from environment, or null if not found</returns>
    public static AwsConfiguration? GetAwsCredentialsFromEnvironment()
    {
        string? accessKeyId = GetEnvironmentVariableWithFallback("AWS_ACCESS_KEY_ID");
        string? secretAccessKey = GetEnvironmentVariableWithFallback("AWS_SECRET_ACCESS_KEY");

        // Need at least an access key to be valid
        if (string.IsNullOrEmpty(accessKeyId))
        {
            return null;
        }

        var config = new AwsConfiguration
        {
            AccessKeyId = accessKeyId,
            SecretAccessKey = secretAccessKey
        };

        // Optional environment variables
        string? sessionToken = GetEnvironmentVariableWithFallback("AWS_SESSION_TOKEN");
        if (!string.IsNullOrEmpty(sessionToken))
        {
            config.SessionToken = sessionToken;
        }

        string? region = GetEnvironmentVariableWithFallback("AWS_DEFAULT_REGION") 
                         ?? GetEnvironmentVariableWithFallback("AWS_REGION");
        if (!string.IsNullOrEmpty(region))
        {
            config.Region = region;
        }

        string? profile = GetEnvironmentVariableWithFallback("AWS_PROFILE");
        if (!string.IsNullOrEmpty(profile))
        {
            config.ProfileName = profile;
        }

        return config;
    }
}
