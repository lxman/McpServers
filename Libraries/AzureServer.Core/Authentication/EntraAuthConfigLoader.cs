using Microsoft.Extensions.Logging;
using System.Text.Json;
using AzureServer.Core.Authentication.models;

namespace AzureServer.Core.Authentication;

/// <summary>
/// Loads Entra authentication configuration from file
/// </summary>
public class EntraAuthConfigLoader(ILogger<EntraAuthConfigLoader> logger)
{
    private const string ConfigFileName = "entra-auth.json";

    /// <summary>
    /// Load configuration from entra-auth.json in the executable directory
    /// Returns default config if file doesn't exist
    /// </summary>
    public EntraAuthConfig LoadConfiguration()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, ConfigFileName);

        if (!File.Exists(configPath))
        {
            logger.LogDebug("Entra auth config file not found at {Path}, using defaults", configPath);
            return CreateDefaultConfig();
        }

        try
        {
            var jsonContent = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<EntraAuthConfig>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

            if (config == null)
            {
                logger.LogWarning("Failed to deserialize Entra auth config, using defaults");
                return CreateDefaultConfig();
            }

            logger.LogInformation("Loaded Entra auth configuration from {Path}", configPath);
            LogConfigurationSummary(config);

            return config;
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Invalid JSON in Entra auth config file, using defaults");
            return CreateDefaultConfig();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading Entra auth config, using defaults");
            return CreateDefaultConfig();
        }
    }

    /// <summary>
    /// Create default configuration
    /// </summary>
    private static EntraAuthConfig CreateDefaultConfig()
    {
        return new EntraAuthConfig
        {
            // Default to common tenant and Azure CLI client ID
            TenantId = "common",
            ClientId = EntraAuthConfig.AzureCliClientId,
            
            // Enable interactive and device code by default
            EnableInteractiveBrowser = true,
            EnableDeviceCode = true,
            
            // Enable token caching
            EnableTokenCache = true,
            TokenCacheName = "azure-mcp-cache",
            
            // Reasonable timeouts
            BrowserTimeoutSeconds = 300,
            DeviceCodeTimeoutSeconds = 300
        };
    }

    /// <summary>
    /// Log summary of loaded configuration (without secrets)
    /// </summary>
    private void LogConfigurationSummary(EntraAuthConfig config)
    {
        logger.LogInformation("Entra Auth Configuration:");
        logger.LogInformation("  Tenant ID: {TenantId}", config.GetEffectiveTenantId());
        logger.LogInformation("  Client ID: {ClientId}", config.GetEffectiveClientId());
        logger.LogInformation("  Interactive Browser: {Enabled}", config.EnableInteractiveBrowser);
        logger.LogInformation("  Device Code: {Enabled}", config.EnableDeviceCode);
        logger.LogInformation("  Managed Identity: {Enabled}", config.EnableManagedIdentity);
        logger.LogInformation("  Service Principal: {Configured}", config.HasClientSecret ? "Configured" : "Not configured");
        logger.LogInformation("  Certificate Auth: {Configured}", config.HasCertificate ? "Configured" : "Not configured");
        logger.LogInformation("  Token Cache: {Enabled} (Name: {Name})", 
            config.EnableTokenCache, 
            config.TokenCacheName ?? "default");
    }

    /// <summary>
    /// Create example configuration file
    /// </summary>
    public void CreateExampleConfigFile()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "entra-auth.example.json");

        var exampleConfig = new
        {
            TenantId = "your-tenant-id-or-common",
            ClientId = "your-client-id-or-leave-for-azure-cli-default",
            
            // Interactive authentication
            EnableInteractiveBrowser = true,
            RedirectUri = "http://localhost:8400",
            BrowserTimeoutSeconds = 300,
            
            // Device code flow
            EnableDeviceCode = true,
            DeviceCodeTimeoutSeconds = 300,
            
            // Service principal (client secret)
            ClientSecret = "your-client-secret-here",
            
            // Certificate authentication
            CertificatePath = "path/to/certificate.pfx",
            CertificatePassword = "certificate-password-if-needed",
            
            // Managed identity
            EnableManagedIdentity = false,
            ManagedIdentityClientId = "user-assigned-mi-client-id-if-needed",
            
            // Token cache settings
            EnableTokenCache = true,
            TokenCacheName = "azure-mcp-cache",
            
            // Advanced settings
            AuthorityHost = "https://login.microsoftonline.com/",
            AdditionalScopes = new[] { "https://management.azure.com/.default" }
        };

        try
        {
            var json = JsonSerializer.Serialize(exampleConfig, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(configPath, json);
            logger.LogInformation("Created example config file at {Path}", configPath);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to create example config file");
        }
    }
}
