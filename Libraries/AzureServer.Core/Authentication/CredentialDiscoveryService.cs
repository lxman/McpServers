using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;

namespace AzureServer.Core.Authentication;

/// <summary>
/// Discovers and tests Azure credentials from multiple sources
/// </summary>
public class CredentialDiscoveryService(ILogger<CredentialDiscoveryService> logger)
{
    /// <summary>
    /// Discover all available Azure credentials from various sources
    /// </summary>
    public async Task<List<CredentialInfo>> DiscoverCredentialsAsync()
    {
        var credentials = new List<CredentialInfo>();

        // Try each credential source independently
        await TryAzureCliCredentialAsync(credentials);
        await TryVisualStudioCredentialAsync(credentials);
        await TryEnvironmentCredentialAsync(credentials);
        await TryAzurePowerShellCredentialAsync(credentials);
        await TrySharedTokenCacheCredentialAsync(credentials);

        return credentials.Where(c => c.IsValid).ToList();
    }

    private async Task TryAzureCliCredentialAsync(List<CredentialInfo> credentials)
    {
        var info = new CredentialInfo
        {
            Id = "azure-cli",
            Source = "Azure CLI"
        };

        try
        {
            var credential = new AzureCliCredential();
            await EnrichCredentialInfoAsync(info, credential);
            
            // Try to get last modified time from Azure CLI cache
            string azurePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".azure");
            string tokenCache = Path.Combine(azurePath, "msal_token_cache.bin");
            
            if (File.Exists(tokenCache))
            {
                info.LastModified = File.GetLastWriteTime(tokenCache);
            }

            credentials.Add(info);
        }
        catch (Exception ex)
        {
            logger.LogDebug("Azure CLI credential not available: {Error}", ex.Message);
            info.IsValid = false;
            info.ErrorMessage = ex.Message;
        }
    }

    private async Task TryVisualStudioCredentialAsync(List<CredentialInfo> credentials)
    {
        var info = new CredentialInfo
        {
            Id = "visual-studio",
            Source = "Visual Studio"
        };

        try
        {
            var credential = new VisualStudioCredential();
            await EnrichCredentialInfoAsync(info, credential);
            
            // Try to get last modified time from VS cache
            string identityServicePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                ".IdentityService");
            string msalCache = Path.Combine(identityServicePath, "msalV2.cache");
            
            if (File.Exists(msalCache))
            {
                info.LastModified = File.GetLastWriteTime(msalCache);
            }

            credentials.Add(info);
        }
        catch (Exception ex)
        {
            logger.LogDebug("Visual Studio credential not available: {Error}", ex.Message);
            info.IsValid = false;
            info.ErrorMessage = ex.Message;
        }
    }

    private async Task TryEnvironmentCredentialAsync(List<CredentialInfo> credentials)
    {
        var info = new CredentialInfo
        {
            Id = "environment",
            Source = "Environment Variables"
        };

        try
        {
            // Check if environment variables are set
            string? clientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
            string? tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
            string? clientSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET");

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(tenantId))
            {
                logger.LogDebug("Environment credential not configured (missing AZURE_CLIENT_ID or AZURE_TENANT_ID)");
                info.IsValid = false;
                info.ErrorMessage = "Environment variables not set";
                return;
            }

            var credential = new EnvironmentCredential();
            await EnrichCredentialInfoAsync(info, credential);
            
            info.Metadata["ClientId"] = clientId;
            credentials.Add(info);
        }
        catch (Exception ex)
        {
            logger.LogDebug("Environment credential not available: {Error}", ex.Message);
            info.IsValid = false;
            info.ErrorMessage = ex.Message;
        }
    }

    private async Task TryAzurePowerShellCredentialAsync(List<CredentialInfo> credentials)
    {
        var info = new CredentialInfo
        {
            Id = "azure-powershell",
            Source = "Azure PowerShell"
        };

        try
        {
            var credential = new AzurePowerShellCredential();
            await EnrichCredentialInfoAsync(info, credential);
            credentials.Add(info);
        }
        catch (Exception ex)
        {
            logger.LogDebug("Azure PowerShell credential not available: {Error}", ex.Message);
            info.IsValid = false;
            info.ErrorMessage = ex.Message;
        }
    }

    private async Task TrySharedTokenCacheCredentialAsync(List<CredentialInfo> credentials)
    {
        var info = new CredentialInfo
        {
            Id = "shared-token-cache",
            Source = "Shared Token Cache"
        };

        try
        {
            var credential = new SharedTokenCacheCredential();
            await EnrichCredentialInfoAsync(info, credential);
            credentials.Add(info);
        }
        catch (Exception ex)
        {
            logger.LogDebug("Shared token cache credential not available: {Error}", ex.Message);
            info.IsValid = false;
            info.ErrorMessage = ex.Message;
        }
    }

    /// <summary>
    /// Enrich credential info by testing it and gathering metadata
    /// </summary>
    private async Task EnrichCredentialInfoAsync(CredentialInfo info, TokenCredential credential)
    {
        try
        {
            // Test the credential by getting a token
            var tokenContext = new TokenRequestContext(["https://management.azure.com/.default"]);
            AccessToken token = await credential.GetTokenAsync(tokenContext, CancellationToken.None);

            info.IsValid = true;

            // Get account and tenant info using Azure Resource Manager
            var armClient = new ArmClient(credential);
            
            // Get tenant information
            try
            {
                TenantCollection? tenants = armClient.GetTenants();
                TenantResource? tenant = tenants.FirstOrDefault();
                if (tenant is not null)
                {
                    info.TenantId = tenant.Data.TenantId?.ToString();
                    info.TenantName = tenant.Data.DisplayName;
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug("Could not get tenant info: {Error}", ex.Message);
            }

            // Get subscription information
            try
            {
                SubscriptionCollection? subscriptions = armClient.GetSubscriptions();
                await foreach (SubscriptionResource? subscription in subscriptions)
                {
                    info.SubscriptionIds.Add(subscription.Data.SubscriptionId ?? string.Empty);
                    
                    // Use the first subscription's display name as an account indicator
                    if (string.IsNullOrEmpty(info.AccountName) && !string.IsNullOrEmpty(subscription.Data.DisplayName))
                    {
                        info.Metadata["FirstSubscription"] = subscription.Data.DisplayName;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug("Could not get subscription info: {Error}", ex.Message);
            }

            // For Azure CLI, try to get the account name from az account show
            if (info.Id == "azure-cli")
            {
                try
                {
                    (bool success, string output) = await ExecuteCommandAsync("az", "account show --query user.name -o tsv");
                    if (success && !string.IsNullOrWhiteSpace(output))
                    {
                        info.AccountName = output.Trim();
                    }
                }
                catch (Exception ex)
                {
                    logger.LogDebug("Could not get Azure CLI account name: {Error}", ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            info.IsValid = false;
            info.ErrorMessage = ex.Message;
            // Don't throw - just mark as invalid and let caller handle gracefully
            logger.LogDebug("Credential validation failed for {Source}: {Error}", info.Source, ex.Message);
        }

    }

    private static async Task<(bool Success, string Output)> ExecuteCommandAsync(string command, string arguments)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            string output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            return (process.ExitCode == 0, output);
        }
        catch
        {
            return (false, string.Empty);
        }
    }
}