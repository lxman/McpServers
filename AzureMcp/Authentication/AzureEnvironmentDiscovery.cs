using System.Text.Json;
using CredentialManagement;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Azure.Identity;
using Azure.Core;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Process = System.Diagnostics.Process;

namespace AzureMcp.Authentication;

/// <summary>
/// Pure environment discovery - no configuration files needed!
/// </summary>
public class AzureEnvironmentDiscovery
{
    private readonly ILogger<AzureEnvironmentDiscovery> _logger;

    public AzureEnvironmentDiscovery(ILogger<AzureEnvironmentDiscovery> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Discovers everything Azure with zero configuration
    /// </summary>
    public async Task<AzureDiscoveryResult> DiscoverAzureEnvironmentsAsync()
    {
        var result = new AzureDiscoveryResult();

        // 1. Discover Azure Resource Manager credentials
        result.AzureCredential = await DiscoverAzureCredentialAsync();

        // 2. Discover Azure CLI status
        result.AzureCliInfo = await DiscoverAzureCliStatusAsync();

        // 3. Discover Azure DevOps environments
        result.DevOpsEnvironments = await DiscoverDevOpsEnvironmentsAsync();

        return result;
    }

    private async Task<TokenCredential?> DiscoverAzureCredentialAsync()
    {
        try
        {
            var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ExcludeInteractiveBrowserCredential = true, // Don't pop up browser in MCP
                ExcludeVisualStudioCodeCredential = false,
                ExcludeAzureCliCredential = false,
                ExcludeEnvironmentCredential = false,
                ExcludeManagedIdentityCredential = false,
                ExcludeSharedTokenCacheCredential = false,
                ExcludeAzurePowerShellCredential = false
            });

            // Validate credential
            var tokenContext = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
            await credential.GetTokenAsync(tokenContext, CancellationToken.None);

            return credential;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("No Azure credential available: {Error}", ex.Message);
            return null;
        }
    }

    private static async Task<AzureCliInfo> DiscoverAzureCliStatusAsync()
    {
        try
        {
            var result = await ExecuteCommandAsync("az", "account show --output json");
            if (result.Success && !string.IsNullOrEmpty(result.Output))
            {
                var accountInfo = JsonSerializer.Deserialize<JsonElement>(result.Output);
                return new AzureCliInfo
                {
                    IsLoggedIn = true,
                    CurrentSubscription = accountInfo.GetProperty("name").GetString(),
                    TenantId = accountInfo.GetProperty("tenantId").GetString(),
                    UserName = accountInfo.GetProperty("user").GetProperty("name").GetString()
                };
            }
        }
        catch
        {
            // Azure CLI not available or not logged in
        }

        return new AzureCliInfo { IsLoggedIn = false };
    }

    private async Task<List<DevOpsEnvironmentInfo>> DiscoverDevOpsEnvironmentsAsync()
    {
        var environments = new List<DevOpsEnvironmentInfo>();

        // 1. Azure CLI DevOps extension
        var cliEnv = await DiscoverFromAzureCliDevOpsAsync();
        if (cliEnv != null) environments.Add(cliEnv);

        // 2. Windows Credential Manager
        environments.AddRange(DiscoverFromCredentialManager());

        // 3. Environment variables
        var envEnv = DiscoverFromEnvironmentVariables();
        if (envEnv != null) environments.Add(envEnv);

        // 4. Azure Pipelines context (if running in pipeline)
        var pipelineEnv = DiscoverFromAzurePipelines();
        if (pipelineEnv != null) environments.Add(pipelineEnv);

        // Remove duplicates and validate
        var validEnvironments = new List<DevOpsEnvironmentInfo>();
        foreach (var env in environments.GroupBy(e => e.OrganizationUrl, StringComparer.OrdinalIgnoreCase)
                                      .Select(g => g.First()))
        {
            if (await ValidateDevOpsEnvironmentAsync(env))
            {
                validEnvironments.Add(env);
            }
        }

        return validEnvironments;
    }

    private async Task<DevOpsEnvironmentInfo?> DiscoverFromAzureCliDevOpsAsync()
    {
        try
        {
            var configResult = await ExecuteCommandAsync("az", "devops configure --list");
            if (configResult.Success && configResult.Output.Contains("organization ="))
            {
                var lines = configResult.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.StartsWith("organization =", StringComparison.OrdinalIgnoreCase))
                    {
                        var url = line.Split('=', 2)[1].Trim();

                        // Try to get a PAT for this organization
                        var pat = await TryGetPatForAzureCliOrgAsync(url);

                        return new DevOpsEnvironmentInfo
                        {
                            OrganizationUrl = url,
                            PersonalAccessToken = pat,
                            Source = "Azure CLI DevOps Extension",
                            HasCredentials = !string.IsNullOrEmpty(pat)
                        };
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Azure CLI DevOps not configured: {Error}", ex.Message);
        }
        return null;
    }

    private static async Task<string?> TryGetPatForAzureCliOrgAsync(string organizationUrl)
    {
        // Extract org name from URL for credential lookup
        var uri = new Uri(organizationUrl);
        var orgName = uri.Segments.LastOrDefault()?.Trim('/');

        // Try common credential patterns
        var targets = new[]
        {
            "AzureDevOps",
            $"AzureDevOps-{orgName}",
            organizationUrl,
            "AZURE_DEVOPS_EXT_PAT"
        };

        foreach (var target in targets)
        {
            var pat = TryGetFromCredentialManager(target) ??
                     Environment.GetEnvironmentVariable(target);
            if (!string.IsNullOrEmpty(pat))
            {
                return pat;
            }
        }

        return Environment.GetEnvironmentVariable("AZURE_DEVOPS_EXT_PAT") ??
               Environment.GetEnvironmentVariable("AZURE_DEVOPS_PAT");
    }

    private List<DevOpsEnvironmentInfo> DiscoverFromCredentialManager()
    {
        var environments = new List<DevOpsEnvironmentInfo>();
        var credentialTargets = new[]
        {
            "AzureDevOps",
            "Azure DevOps",
            "VSTS",
            "dev.azure.com",
            "visualstudio.com"
        };

        foreach (var target in credentialTargets)
        {
            try
            {
                using var cred = new Credential();
                cred.Target = target;
                if (cred.Load() && !string.IsNullOrEmpty(cred.Password))
                {
                    var orgUrl = ExtractOrgUrlFromCredential(cred);
                    if (!string.IsNullOrEmpty(orgUrl))
                    {
                        environments.Add(new DevOpsEnvironmentInfo
                        {
                            OrganizationUrl = orgUrl,
                            PersonalAccessToken = cred.Password,
                            Source = $"Credential Manager ({target})",
                            HasCredentials = true,
                            CredentialTarget = target
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Could not read credential {Target}: {Error}", target, ex.Message);
            }
        }

        return environments;
    }

    private static DevOpsEnvironmentInfo? DiscoverFromEnvironmentVariables()
    {
        var patVars = new[] { "AZURE_DEVOPS_EXT_PAT", "AZURE_DEVOPS_PAT", "SYSTEM_ACCESSTOKEN" };
        var orgVars = new[] { "AZURE_DEVOPS_ORG_URL", "SYSTEM_COLLECTIONURI" };

        string? pat = null;
        string? patSource = null;
        foreach (var varName in patVars)
        {
            pat = Environment.GetEnvironmentVariable(varName);
            if (!string.IsNullOrEmpty(pat))
            {
                patSource = varName;
                break;
            }
        }

        string? orgUrl = null;
        foreach (var varName in orgVars)
        {
            orgUrl = Environment.GetEnvironmentVariable(varName);
            if (!string.IsNullOrEmpty(orgUrl)) break;
        }

        if (!string.IsNullOrEmpty(pat) && !string.IsNullOrEmpty(orgUrl))
        {
            return new DevOpsEnvironmentInfo
            {
                OrganizationUrl = orgUrl,
                PersonalAccessToken = pat,
                Source = $"Environment Variables ({patSource})",
                HasCredentials = true
            };
        }

        return null;
    }

    private static DevOpsEnvironmentInfo? DiscoverFromAzurePipelines()
    {
        // Check if running in Azure Pipelines
        var systemAccessToken = Environment.GetEnvironmentVariable("SYSTEM_ACCESSTOKEN");
        var collectionUri = Environment.GetEnvironmentVariable("SYSTEM_COLLECTIONURI");

        if (!string.IsNullOrEmpty(systemAccessToken) && !string.IsNullOrEmpty(collectionUri))
        {
            return new DevOpsEnvironmentInfo
            {
                OrganizationUrl = collectionUri,
                PersonalAccessToken = systemAccessToken,
                Source = "Azure Pipelines System Token",
                HasCredentials = true
            };
        }

        return null;
    }

    private static string? ExtractOrgUrlFromCredential(Credential credential)
    {
        // Smart extraction from credential fields
        var possibleSources = new[] { credential.Username, credential.Target, credential.Description };

        foreach (var source in possibleSources)
        {
            if (string.IsNullOrEmpty(source)) continue;

            // Direct URL
            if (Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
                (uri.Host.Contains("dev.azure.com") || uri.Host.Contains("visualstudio.com")))
            {
                return uri.ToString().TrimEnd('/');
            }

            // Organization name pattern
            if (source.Contains('/') && (source.Contains("dev.azure.com") || source.Contains("visualstudio.com")))
            {
                var parts = source.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var orgName = parts.LastOrDefault();
                if (!string.IsNullOrEmpty(orgName) && !orgName.Contains(".com"))
                {
                    return $"https://dev.azure.com/{orgName}";
                }
            }
        }

        // Fallback: Return null - user will get helpful error message
        return null;
    }

    private async Task<bool> ValidateDevOpsEnvironmentAsync(DevOpsEnvironmentInfo environment)
    {
        if (string.IsNullOrEmpty(environment.PersonalAccessToken))
            return false;

        try
        {
            var credentials = new VssBasicCredential(string.Empty, environment.PersonalAccessToken);
            using var connection = new VssConnection(new Uri(environment.OrganizationUrl), credentials);
            var client = connection.GetClient<ProjectHttpClient>();

            // Test connection by getting projects
            await client.GetProjects();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Environment validation failed for {Org}: {Error}",
                environment.OrganizationUrl, ex.Message);
            return false;
        }
    }

    private static string? TryGetFromCredentialManager(string target)
    {
        try
        {
            using var cred = new Credential();
            cred.Target = target;
            return cred.Load() ? cred.Password : null;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<(bool Success, string Output)> ExecuteCommandAsync(string command, string arguments)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            return (process.ExitCode == 0, output);
        }
        catch
        {
            return (false, string.Empty);
        }
    }
}

// Data classes
public class AzureDiscoveryResult
{
    public TokenCredential? AzureCredential { get; set; }
    public List<DevOpsEnvironmentInfo> DevOpsEnvironments { get; set; } = new();
    public AzureCliInfo AzureCliInfo { get; set; } = new();
}

public class DevOpsEnvironmentInfo
{
    public string OrganizationUrl { get; set; } = string.Empty;
    public string? PersonalAccessToken { get; set; }
    public string Source { get; set; } = string.Empty;
    public bool HasCredentials { get; set; }
    public string? CredentialTarget { get; set; }
}

public class AzureCliInfo
{
    public bool IsLoggedIn { get; set; }
    public string? CurrentSubscription { get; set; }
    public string? TenantId { get; set; }
    public string? UserName { get; set; }
}
