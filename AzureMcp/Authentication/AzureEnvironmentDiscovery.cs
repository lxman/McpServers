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
    private readonly string _logPath = Path.Combine(AppContext.BaseDirectory, "azure-discovery.log");

    public AzureEnvironmentDiscovery(ILogger<AzureEnvironmentDiscovery> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Discovers Azure DevOps environments independently (no Azure RM required!)
    /// </summary>
    public async Task<AzureDiscoveryResult> DiscoverAzureEnvironmentsAsync()
    {
        var result = new AzureDiscoveryResult();
        
        // Log to file in project directory for debugging
        await File.WriteAllTextAsync(_logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Starting Azure DevOps discovery...\n");
        
        try
        {
            // Focus ONLY on DevOps discovery - Azure RM is optional
            _logger.LogInformation("🔍 Discovering Azure DevOps environments...");
            await AddLogLine("Starting Azure DevOps discovery...");
            
            result.DevOpsEnvironments = await DiscoverDevOpsEnvironmentsAsync();

            await AddLogLine($"Found {result.DevOpsEnvironments.Count} DevOps environments");
            
            foreach (DevOpsEnvironmentInfo env in result.DevOpsEnvironments)
            {
                await AddLogLine($"\t{env.OrganizationUrl} via {env.Source} (HasCredentials: {env.HasCredentials})");
            }
            
            // Azure RM is optional - only try if user wants it
            if (result.DevOpsEnvironments.Count > 0)
            {
                _logger.LogInformation("✅ Azure DevOps discovery successful");
                await AddLogLine("DevOps discovery successful!");
            }
            else
            {
                _logger.LogWarning("❌ No Azure DevOps environments found");
                await AddLogLine("No Azure DevOps environments found");
            }
            
            // Try Azure RM as bonus feature (don't fail if this doesn't work)
            try
            {
                result.AzureCredential = await DiscoverAzureCredentialAsync();
                if (result.AzureCredential != null)
                {
                    await AddLogLine("Azure RM credentials available");
                }
            }
            catch (Exception ex)
            {
                await AddLogLine($"Azure RM discovery failed: {ex.Message}");
            }
            
            // Azure CLI status for info only
            try
            {
                result.AzureCliInfo = await DiscoverAzureCliStatusAsync();
            }
            catch (Exception ex)
            {
                await AddLogLine($"Azure CLI status check failed: {ex.Message}");
                result.AzureCliInfo = new AzureCliInfo { IsLoggedIn = false };
            }
        }
        catch (Exception ex)
        {
            await AddLogLine($"Discovery failed: {ex}");
            throw;
        }

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
            var tokenContext = new TokenRequestContext(["https://management.azure.com/.default"]);
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
            (bool Success, string Output) result = await ExecuteCommandAsync("az", "account show --output json");
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
        
        // 0: Local config file (bypass all process context issues)
        await AddLogLine("Trying local config file...");
        DevOpsEnvironmentInfo? fileEnv = await DiscoverFromLocalConfigFile();
        if (fileEnv != null) 
        {
            await AddLogLine($"Found config file environment: {fileEnv.OrganizationUrl}");
            environments.Add(fileEnv);
        }
        else
        {
            await AddLogLine("No local config file found");
        }


        await AddLogLine("Calling DiscoverDevOpsEnvironmentsAsync...");
        // 1. Azure CLI DevOps extension
        DevOpsEnvironmentInfo? cliEnv = await DiscoverFromAzureCliDevOpsAsync();
        await AddLogLine($"{(cliEnv is not null ? "Found" : "No")} Azure CLI DevOps environment");
        if (cliEnv != null) environments.Add(cliEnv);

        int envLength = environments.Count;
        await AddLogLine("Calling DiscoverFromCredentialManager...");
        // 2. Windows Credential Manager
        environments.AddRange(await DiscoverFromCredentialManager());
        await AddLogLine($"Found {environments.Count - envLength} environments from Credential Manager");

        await AddLogLine("Calling DiscoverFromEnvironmentVariables...");
        // 3. Environment variables
        DevOpsEnvironmentInfo? envEnv = await DiscoverFromEnvironmentVariables();
        await AddLogLine($"{(envEnv is not null ? "Found" : "No")} environment variable DevOps environment");
        if (envEnv != null) environments.Add(envEnv);

        await AddLogLine("Calling DiscoverFromAzurePipelines...");
        // 4. Azure Pipelines context (if running in pipeline)
        DevOpsEnvironmentInfo? pipelineEnv = DiscoverFromAzurePipelines();
        await AddLogLine($"{(pipelineEnv is not null ? "Found" : "No")} Azure Pipelines DevOps environment");
        if (pipelineEnv != null) environments.Add(pipelineEnv);

        // Remove duplicates and validate
        var validEnvironments = new List<DevOpsEnvironmentInfo>();
        foreach (DevOpsEnvironmentInfo env in environments.GroupBy(e => e.OrganizationUrl, StringComparer.OrdinalIgnoreCase)
                     .Select(g => g.First()))
        {
            if (await ValidateDevOpsEnvironmentAsync(env))
            {
                validEnvironments.Add(env);
            }
        }

        return validEnvironments;
    }
    
    private async Task<DevOpsEnvironmentInfo?> DiscoverFromLocalConfigFile()
    {
        try
        {
            string configPath = Path.Combine(AppContext.BaseDirectory, "devops-config.json");
            await AddLogLine($"Looking for config file at: {configPath}");
        
            if (File.Exists(configPath))
            {
                await AddLogLine("Config file exists, reading...");
                string content = await File.ReadAllTextAsync(configPath);
                await AddLogLine($"Config file content: {content}");
            
                var config = JsonSerializer.Deserialize<JsonElement>(content);
                JsonElement azureDevOps = config.GetProperty("AzureDevOps");
            
                string? orgUrl = azureDevOps.GetProperty("OrganizationUrl").GetString();
                string? pat = azureDevOps.GetProperty("PersonalAccessToken").GetString();
            
                if (!string.IsNullOrEmpty(orgUrl) && !string.IsNullOrEmpty(pat))
                {
                    await AddLogLine($"Successfully parsed config: {orgUrl}");
                    return new DevOpsEnvironmentInfo
                    {
                        OrganizationUrl = orgUrl,
                        PersonalAccessToken = pat,
                        Source = "Local Config File",
                        HasCredentials = true
                    };
                }
            }
            else
            {
                await AddLogLine("Config file does not exist");
            }
        }
        catch (Exception ex)
        {
            await AddLogLine($"Config file discovery failed: {ex.Message}");
        }
    
        return null;
    }

    private async Task<DevOpsEnvironmentInfo?> DiscoverFromAzureCliDevOpsAsync()
    {
        try
        {
            (bool Success, string Output) configResult = await ExecuteCommandAsync("az", "devops configure --list");
            await AddLogLine($"configResult: Success={configResult.Success}, Output={configResult.Output}");
            if (configResult.Success && configResult.Output.Contains("organization ="))
            {
                string[] lines = configResult.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    if (line.StartsWith("organization =", StringComparison.OrdinalIgnoreCase))
                    {
                        string url = line.Split('=', 2)[1].Trim();
                        await AddLogLine($"Found Azure CLI DevOps organization: {url}");

                        // Try to get a PAT for this organization
                        string? pat = await TryGetPatForAzureCliOrgAsync(url);

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

    private async Task<string?> TryGetPatForAzureCliOrgAsync(string organizationUrl)
    {
        // Extract org name from URL for credential lookup
        var uri = new Uri(organizationUrl);
        string? orgName = uri.Segments.LastOrDefault()?.Trim('/');

        // Try common credential patterns
        string[] targets = new[]
        {
            "AzureDevOps",
            $"AzureDevOps-{orgName}",
            organizationUrl,
            "AZURE_DEVOPS_EXT_PAT"
        };

        foreach (string target in targets)
        {
            await AddLogLine($"Attempting to get PAT from Credential Manager or env var: {target}");
            string? pat = TryGetFromCredentialManager(target) ??
                          Environment.GetEnvironmentVariable(target);
            if (!string.IsNullOrEmpty(pat))
            {
                await AddLogLine($"Recovered PAT from {target} - {pat}");
                return pat;
            }
            else
            {
                await AddLogLine("No PAT found in this source");
            }
        }

        return Environment.GetEnvironmentVariable("AZURE_DEVOPS_EXT_PAT") ??
               Environment.GetEnvironmentVariable("AZURE_DEVOPS_PAT");
    }

    private async Task<List<DevOpsEnvironmentInfo>> DiscoverFromCredentialManager()
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

        foreach (string target in credentialTargets)
        {
            await AddLogLine($"Checking Credential Manager for target: {target}");
            try
            {
                using var cred = new Credential();
                cred.Target = target;
                await AddLogLine($"Found credential target: {cred.Target}, Username: {cred.Username}, Description: {cred.Description}, Password: {(string.IsNullOrEmpty(cred.Password) ? "No" : "Yes")}");
                if (cred.Load() && !string.IsNullOrEmpty(cred.Password))
                {
                    string? orgUrl = ExtractOrgUrlFromCredential(cred);
                    await AddLogLine($"Extracted Org URL: {orgUrl}");
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

    private async Task<DevOpsEnvironmentInfo?> DiscoverFromEnvironmentVariables()
    {
        var patVars = new[] { "AZURE_DEVOPS_EXT_PAT", "AZURE_DEVOPS_PAT", "SYSTEM_ACCESSTOKEN" };
        var orgVars = new[] { "AZURE_DEVOPS_ORG_URL", "SYSTEM_COLLECTIONURI" };

        string? pat = null;
        string? patSource = null;
        foreach (string varName in patVars)
        {
            await AddLogLine($"Trying to get PAT from env var: {varName}");
            pat = Environment.GetEnvironmentVariable(varName);
            await AddLogLine($"Found PAT: {(string.IsNullOrEmpty(pat) ? "No" : "Yes")}");
            if (!string.IsNullOrEmpty(pat))
            {
                patSource = varName;
                break;
            }
        }

        string? orgUrl = null;
        foreach (string varName in orgVars)
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
        string? systemAccessToken = Environment.GetEnvironmentVariable("SYSTEM_ACCESSTOKEN");
        string? collectionUri = Environment.GetEnvironmentVariable("SYSTEM_COLLECTIONURI");

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
        string[] possibleSources = new[] { credential.Username, credential.Target, credential.Description };

        foreach (string source in possibleSources)
        {
            if (string.IsNullOrEmpty(source)) continue;

            // Direct URL
            if (Uri.TryCreate(source, UriKind.Absolute, out Uri? uri) &&
                (uri.Host.Contains("dev.azure.com") || uri.Host.Contains("visualstudio.com")))
            {
                return uri.ToString().TrimEnd('/');
            }

            // Organization name pattern
            if (source.Contains('/') && (source.Contains("dev.azure.com") || source.Contains("visualstudio.com")))
            {
                string[] parts = source.Split('/', StringSplitOptions.RemoveEmptyEntries);
                string? orgName = parts.LastOrDefault();
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
    
    private async Task AddLogLine(string message)
    {
        var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n";
        await File.AppendAllTextAsync(_logPath, logEntry);
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

// Data classes
public class AzureDiscoveryResult
{
    public TokenCredential? AzureCredential { get; set; }
    public List<DevOpsEnvironmentInfo> DevOpsEnvironments { get; set; } = [];
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
