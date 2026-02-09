using System.ComponentModel;
using System.Text.Json;
using Mcp.Common.Core;
using Azure.Core;
using AzureServer.Core.Authentication;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace AzureMcp.McpTools;

/// <summary>
/// MCP tools for Azure Credential Management operations
/// </summary>
[McpServerToolType]
public class CredentialManagementTools(
    CredentialSelectionService selectionService,
    ILogger<CredentialManagementTools> logger)
{
    [McpServerTool, DisplayName("list_credentials")]
    [Description("List available Azure credentials. See skills/azure/credentialmanagement/list-credentials.md only when using this tool")]
    public async Task<string> ListCredentials()
    {
        try
        {
            logger.LogDebug("Listing credentials");
            (_, CredentialSelectionResult result) = await selectionService.GetCredentialAsync();

            if (result.Status == SelectionStatus.NoCredentialsFound)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = "No Azure credentials found",
                    instructions = new[]
                    {
                        "Azure CLI: Run 'az login'",
                        "Visual Studio: Sign in to Visual Studio",
                        "Environment Variables: Set AZURE_CLIENT_ID, AZURE_TENANT_ID, and AZURE_CLIENT_SECRET",
                        "Azure PowerShell: Run 'Connect-AzAccount'"
                    }
                }, SerializerOptions.JsonOptionsIndented);
            }

            List<CredentialInfo> credentials =
                result.AvailableCredentials
                ?? (result.SelectedCredential is not null ? [result.SelectedCredential] : []);

            if (credentials.Count == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = "No credentials available"
                }, SerializerOptions.JsonOptionsIndented);
            }

            CredentialInfo? selected = selectionService.GetSelectedCredential();
            return JsonSerializer.Serialize(new
            {
                success = true,
                count = credentials.Count,
                credentials = credentials.Select((c, i) => new
                {
                    index = i + 1,
                    id = c.Id,
                    source = c.Source,
                    accountName = c.AccountName,
                    tenantId = c.TenantId,
                    tenantName = c.TenantName,
                    subscriptionCount = c.SubscriptionCount,
                    isSelected = selected?.Id == c.Id
                }).ToArray(),
                currentlySelected = selected?.Source
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing credentials");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "ListCredentials",
                type = ex.GetType().Name
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("select_credential")]
    [Description("Select specific Azure credential. See skills/azure/credentialmanagement/select-credential.md only when using this tool")]
    public string SelectCredential(string credentialId)
    {
        try
        {
            logger.LogDebug("Selecting credential {CredentialId}", credentialId);
            (TokenCredential? credential, CredentialSelectionResult result) = selectionService.SelectCredential(credentialId);

            if (result.Status == SelectionStatus.Error)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = result.ErrorMessage
                }, SerializerOptions.JsonOptionsIndented);
            }

            if (result.Status != SelectionStatus.Selected || result.SelectedCredential is null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Failed to select credential"
                }, SerializerOptions.JsonOptionsIndented);
            }

            CredentialInfo? info = result.SelectedCredential;
            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "Credential selected successfully",
                credential = new
                {
                    id = info.Id,
                    source = info.Source,
                    accountName = info.AccountName,
                    tenantId = info.TenantId,
                    tenantName = info.TenantName,
                    subscriptionCount = info.SubscriptionCount
                }
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error selecting credential");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "SelectCredential",
                type = ex.GetType().Name
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("get_current_credential")]
    [Description("Get currently selected credential. See skills/azure/credentialmanagement/get-current-credential.md only when using this tool")]
    public string GetCurrentCredential()
    {
        try
        {
            logger.LogDebug("Getting current credential");
            CredentialInfo? selected = selectionService.GetSelectedCredential();

            if (selected is null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = "No credential selected",
                    instructions = "Use list_credentials to see available credentials, then select_credential to choose one"
                }, SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                credential = new
                {
                    id = selected.Id,
                    source = selected.Source,
                    accountName = selected.AccountName,
                    tenantId = selected.TenantId,
                    tenantName = selected.TenantName,
                    subscriptionCount = selected.SubscriptionCount
                }
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting current credential");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "GetCurrentCredential",
                type = ex.GetType().Name
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("clear_credential_selection")]
    [Description("Clear credential selection. See skills/azure/credentialmanagement/clear-credential-selection.md only when using this tool")]
    public string ClearCredentialSelection()
    {
        try
        {
            logger.LogDebug("Clearing credential selection");
            selectionService.ClearSelection();

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "Credential selection cleared",
                note = "The next Azure operation will discover and select credentials again"
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error clearing credential selection");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "ClearCredentialSelection",
                type = ex.GetType().Name
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("get_credentials_help")]
    [Description("Get help for Azure credentials. See skills/azure/credentialmanagement/get-credentials-help.md only when using this tool")]
    public string GetCredentialsHelp()
    {
        return JsonSerializer.Serialize(new
        {
            success = true,
            title = "Azure Credential Management Help",
            sources = new[]
            {
                new
                {
                    name = "Azure CLI (Recommended)",
                    install = "https://docs.microsoft.com/cli/azure/install-azure-cli",
                    login = "Run 'az login' in your terminal",
                    bestFor = "Personal development and testing"
                },
                new
                {
                    name = "Visual Studio",
                    install = "Sign in to Visual Studio (Tools > Options > Azure Service Authentication)",
                    login = "",
                    bestFor = "Development on Windows with Visual Studio"
                },
                new
                {
                    name = "Environment Variables",
                    install = "Set: AZURE_CLIENT_ID, AZURE_TENANT_ID, AZURE_CLIENT_SECRET",
                    login = "",
                    bestFor = "Service principals and automated scenarios"
                },
                new
                {
                    name = "Azure PowerShell",
                    install = "https://docs.microsoft.com/powershell/azure/install-az-ps",
                    login = "Run 'Connect-AzAccount'",
                    bestFor = "PowerShell users"
                }
            },
            commands = new[]
            {
                new { tool = "list_credentials", description = "List all available credentials" },
                new { tool = "select_credential", description = "Select a specific credential" },
                new { tool = "get_current_credential", description = "Get currently selected credential" },
                new { tool = "clear_credential_selection", description = "Clear credential selection" }
            },
            learnMore = "https://learn.microsoft.com/azure/developer/intro/azure-developer-authentication"
        }, SerializerOptions.JsonOptionsIndented);
    }

    [McpServerTool, DisplayName("test_credential")]
    [Description("Test current credential by attempting to get subscriptions. See skills/azure/credentialmanagement/test-credential.md only when using this tool")]
    public async Task<string> TestCredential()
    {
        try
        {
            logger.LogDebug("Testing credential");
            (TokenCredential? credential, CredentialSelectionResult result) = await selectionService.GetCredentialAsync();

            if (result.Status == SelectionStatus.NoCredentialsFound)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = "No credentials found to test",
                    instructions = "Use list_credentials to see available credentials"
                }, SerializerOptions.JsonOptionsIndented);
            }

            if (result.Status == SelectionStatus.Error)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = result.ErrorMessage
                }, SerializerOptions.JsonOptionsIndented);
            }

            CredentialInfo? selected = result.SelectedCredential;
            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "Credential is valid and working",
                credential = new
                {
                    source = selected?.Source,
                    accountName = selected?.AccountName,
                    tenantId = selected?.TenantId,
                    subscriptionCount = selected?.SubscriptionCount
                }
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error testing credential");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "TestCredential",
                type = ex.GetType().Name
            }, SerializerOptions.JsonOptionsIndented);
        }
    }
}
