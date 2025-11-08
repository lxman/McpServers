using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using AzureServer.Core.Authentication.models;
using Meziantou.Framework.Win32;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using RegistryTools;
using Process = System.Diagnostics.Process;
#pragma warning disable CS0618 // Type or member is obsolete

namespace AzureServer.Core.Authentication;

/// <summary>
/// Pure environment discovery - no configuration files needed!
/// </summary>
[SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
public class AzureEnvironmentDiscovery(ILogger<AzureEnvironmentDiscovery> logger)
{
    private readonly string _logPath = Path.Combine(AppContext.BaseDirectory, "azure-discovery.log");

    // Registry manager for reading system environment variables (read-only mode for safety)
    private static readonly Lazy<RegistryManager> RegistryManager = new(() => 
        new RegistryManager(RegistryAccessMode.ReadOnly));

    /// <summary>
    /// Gets an environment variable value from either the process environment or the system registry.
    /// This is needed because when Claude starts the server, it doesn't inherit system environment variables.
    /// </summary>
    /// <param name="variableName">The name of the environment variable</param>
    /// <returns>The value of the environment variable, or null if not found</returns>
    private static async Task<string?> GetEnvironmentVariableWithRegistryFallback(string variableName)
    {
        // First, try the process environment (fast path)
        string? value = Environment.GetEnvironmentVariable(variableName);
        if (!string.IsNullOrEmpty(value))
        {
            return value;
        }

        // Fallback to registry (slower, but works when process env is not inherited)
        try
        {
            const string systemEnvPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Environment";
            value = RegistryManager.Value.ReadValue(systemEnvPath, variableName)?.ToString();
            return value;
        }
        catch
        {
            // Registry read failed, return null
            return null;
        }
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
            logger.LogInformation("🔍 Discovering Azure DevOps environments...");
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
                logger.LogInformation("✅ Azure DevOps discovery successful");
                await AddLogLine("DevOps discovery successful!");
            }
            else
            {
                logger.LogWarning("❌ No Azure DevOps environments found");
                await AddLogLine("No Azure DevOps environments found");
            }
            
            // Try Azure RM as bonus feature (don't fail if this doesn't work)
            try
            {
                result.AzureCredential = await DiscoverAzureCredentialAsync();
                if (result.AzureCredential is not null)
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
            logger.LogError(ex, "Azure environment discovery encountered an error, but returning partial results");
            // Don't throw - return whatever partial results we have
            // This allows the app to start even if discovery fails
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
            logger.LogDebug("No Azure credential available: {Error}", ex.Message);
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

    public async Task<List<DevOpsEnvironmentInfo>> DiscoverDevOpsEnvironmentsAsync()
    {
        var environments = new List<DevOpsEnvironmentInfo>();
        
        // 0: Local config file (bypass all process context issues)
        await AddLogLine("Trying local config file...");
        DevOpsEnvironmentInfo? fileEnv = await DiscoverFromLocalConfigFile();
        if (fileEnv is not null) 
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
        if (cliEnv is not null) environments.Add(cliEnv);

        int envLength = environments.Count;
        await AddLogLine("Calling DiscoverFromCredentialManager...");
        // 2. Windows Credential Manager
        environments.AddRange(await DiscoverFromCredentialManager());
        await AddLogLine($"Found {environments.Count - envLength} environments from Credential Manager");

        await AddLogLine("Calling DiscoverFromEnvironmentVariables...");
        // 3. Environment variables
        DevOpsEnvironmentInfo? envEnv = await DiscoverFromEnvironmentVariables();
        await AddLogLine($"{(envEnv is not null ? "Found" : "No")} environment variable DevOps environment");
        if (envEnv is not null) environments.Add(envEnv);

        await AddLogLine("Calling DiscoverFromAzurePipelines...");
        // 4. Azure Pipelines context (if running in pipeline)
        DevOpsEnvironmentInfo? pipelineEnv = await DiscoverFromAzurePipelines();

        await AddLogLine($"{(pipelineEnv is not null ? "Found" : "No")} Azure Pipelines DevOps environment");
        if (pipelineEnv is not null) environments.Add(pipelineEnv);

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
                    if (!line.StartsWith("organization =", StringComparison.OrdinalIgnoreCase)) continue;
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
        catch (Exception ex)
        {
            logger.LogDebug("Azure CLI DevOps not configured: {Error}", ex.Message);
        }
        return null;
    }

    private async Task<string?> TryGetPatForAzureCliOrgAsync(string organizationUrl)
    {
        // Extract org name from URL for credential lookup
        var uri = new Uri(organizationUrl);
        string? orgName = uri.Segments.LastOrDefault()?.Trim('/');

        // Try common credential patterns
        string[] targets =
        [
            "AzureDevOps",
            $"AzureDevOps-{orgName}",
            organizationUrl,
            "AZURE_DEVOPS_EXT_PAT"
        ];

        foreach (string target in targets)
        {
            await AddLogLine($"Attempting to get PAT from Credential Manager or env var: {target}");
            string? pat = TryGetFromCredentialManager(target) ??
                          await GetEnvironmentVariableWithRegistryFallback(target);
            if (!string.IsNullOrEmpty(pat))
            {
                await AddLogLine($"Recovered PAT from {target} - {pat}");
                return pat;
            }

            await AddLogLine("No PAT found in this source");
        }

        return await GetEnvironmentVariableWithRegistryFallback("AZURE_DEVOPS_EXT_PAT") ??
               await GetEnvironmentVariableWithRegistryFallback("AZURE_DEVOPS_PAT");
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
                Credential? cred = CredentialManager.ReadCredential(target);
                if (cred != null && !string.IsNullOrEmpty(cred.Password))
                {
                    await AddLogLine($"Found credential target: {target}, Username: {cred.UserName}, Password: Yes");
                    string? orgUrl = ExtractOrgUrlFromCredential(cred, target);
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
                logger.LogDebug("Could not read credential {Target}: {Error}", target, ex.Message);
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
            pat = await GetEnvironmentVariableWithRegistryFallback(varName);
            await AddLogLine($"Found PAT: {(string.IsNullOrEmpty(pat) ? "No" : "Yes")}");
            if (string.IsNullOrEmpty(pat)) continue;
            patSource = varName;
            break;
        }

        string? orgUrl = null;
        foreach (string varName in orgVars)
        {
            orgUrl = await GetEnvironmentVariableWithRegistryFallback(varName);
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

    private static async Task<DevOpsEnvironmentInfo?> DiscoverFromAzurePipelines()
    {
        // Check if running in Azure Pipelines
        string? systemAccessToken = await GetEnvironmentVariableWithRegistryFallback("SYSTEM_ACCESSTOKEN");
        string? collectionUri = await GetEnvironmentVariableWithRegistryFallback("SYSTEM_COLLECTIONURI");

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

    private static string? ExtractOrgUrlFromCredential(Credential credential, string target)
    {
        // Smart extraction from credential fields
        string[] possibleSources = [credential.UserName ?? string.Empty, target, credential.Comment ?? string.Empty];

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
            if (!source.Contains('/') ||
                (!source.Contains("dev.azure.com") && !source.Contains("visualstudio.com"))) continue;
            string[] parts = source.Split('/', StringSplitOptions.RemoveEmptyEntries);
            string? orgName = parts.LastOrDefault();
            if (!string.IsNullOrEmpty(orgName) && !orgName.Contains(".com"))
            {
                return $"https://dev.azure.com/{orgName}";
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
            logger.LogDebug("Environment validation failed for {Org}: {Error}",
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
            Credential? cred = CredentialManager.ReadCredential(target);
            return cred?.Password;
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