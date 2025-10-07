using System.ComponentModel;
using Azure.Core;
using AzureMcp.Authentication;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace AzureMcp.Tools;

/// <summary>
/// Tools for managing Azure credentials and authentication
/// </summary>
[McpServerToolType]
public class CredentialManagementTools(
    ILogger<CredentialManagementTools> logger,
    CredentialSelectionService selectionService)
{
    /// <summary>
    /// List all available Azure credentials with detailed information
    /// </summary>
    [McpServerTool]
    [Description("List all available Azure credentials discovered on this system")]
    public async Task<string> ListCredentialsAsync()
    {
        try
        {
            (_, CredentialSelectionResult result) = await selectionService.GetCredentialAsync();

            if (result.Status == SelectionStatus.NoCredentialsFound)
            {
                return "❌ No Azure credentials found.\n\n" +
                       "Please authenticate using one of these methods:\n" +
                       "1. Azure CLI: Run 'az login'\n" +
                       "2. Visual Studio: Sign in to Visual Studio\n" +
                       "3. Environment Variables: Set AZURE_CLIENT_ID, AZURE_TENANT_ID, and AZURE_CLIENT_SECRET\n" +
                       "4. Azure PowerShell: Run 'Connect-AzAccount'";
            }

            List<CredentialInfo> credentials = result.AvailableCredentials ?? 
                                               (result.SelectedCredential is not null ? [result.SelectedCredential] : []);

            if (credentials.Count == 0)
            {
                return "No credentials available.";
            }

            var output = new List<string>
            {
                $"🔐 Found {credentials.Count} Azure credential{(credentials.Count > 1 ? "s" : "")}:",
                ""
            };

            for (var i = 0; i < credentials.Count; i++)
            {
                output.Add(credentials[i].FormatForDisplay(i + 1));
                output.Add("");
            }

            CredentialInfo? selected = selectionService.GetSelectedCredential();
            if (selected is not null)
            {
                output.Add($"✅ Currently using: {selected.Source}");
            }
            else if (credentials.Count > 1)
            {
                output.Add("ℹ️  Multiple credentials available. Use azure:select_credential to choose one.");
            }

            return string.Join(Environment.NewLine, output);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing credentials");
            return $"❌ Error listing credentials: {ex.Message}";
        }
    }

    /// <summary>
    /// Select a specific Azure credential to use for this session
    /// </summary>
    [McpServerTool]
    [Description("Select a specific Azure credential to use for this session by credential ID")]
    public string SelectCredential(
        [Description("Credential ID to use (e.g., 'azure-cli', 'visual-studio', 'environment', 'azure-powershell', 'shared-token-cache')")]
        string credentialId)
    {
        try
        {
            (TokenCredential? credential, CredentialSelectionResult result) = selectionService.SelectCredential(credentialId);

            if (result.Status == SelectionStatus.Error)
            {
                return $"❌ Error: {result.ErrorMessage}";
            }

            if (result.Status != SelectionStatus.Selected || result.SelectedCredential is null)
                return "❌ Failed to select credential";
            CredentialInfo? info = result.SelectedCredential;
            return $"✅ Selected {info.Source} credential\n\n" +
                   $"Account: {info.AccountName ?? "Unknown"}\n" +
                   $"Tenant: {info.TenantName ?? info.TenantId ?? "Unknown"}\n" +
                   $"Subscriptions: {info.SubscriptionCount} available\n\n" +
                   $"This credential will be used for all Azure operations in this session.";

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error selecting credential");
            return $"❌ Error selecting credential: {ex.Message}";
        }
    }

    /// <summary>
    /// Get the currently selected Azure credential
    /// </summary>
    [McpServerTool]
    [Description("Get information about the currently selected Azure credential")]
    public string GetCurrentCredential()
    {
        try
        {
            CredentialInfo? selected = selectionService.GetSelectedCredential();
            
            if (selected is null)
            {
                return "❌ No credential selected.\n\n" +
                       "Use azure:list_credentials to see available credentials, then azure:select_credential to choose one.";
            }

            return $"✅ Current Credential:\n\n" +
                   $"{selected.FormatForDisplay(1).Replace("1. ", "")}";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting current credential");
            return $"❌ Error getting current credential: {ex.Message}";
        }
    }

    /// <summary>
    /// Clear the current credential selection and force re-selection
    /// </summary>
    [McpServerTool]
    [Description("Clear the current credential selection. The next Azure operation will trigger credential discovery and selection again.")]
    public string ClearCredentialSelection()
    {
        try
        {
            selectionService.ClearSelection();
            return "✅ Credential selection cleared.\n\n" +
                   "The next Azure operation will discover and select credentials again.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error clearing credential selection");
            return $"❌ Error clearing credential selection: {ex.Message}";
        }
    }

    /// <summary>
    /// Get help information about Azure credential management
    /// </summary>
    [McpServerTool]
    [Description("Get help with finding and configuring Azure credentials - start here if you're unsure about your setup")]
    public static string GetCredentialsHelp()
    {
        return """
               🔐 Azure Credential Management Help

               The AzureMcp server can automatically discover Azure credentials from multiple sources:

               1️⃣  Azure CLI (Recommended)
                  - Install: https://docs.microsoft.com/cli/azure/install-azure-cli
                  - Login: Run 'az login' in your terminal
                  - Best for: Personal development and testing

               2️⃣  Visual Studio
                  - Sign in to Visual Studio (Tools > Options > Azure Service Authentication)
                  - Best for: Development on Windows with Visual Studio

               3️⃣  Environment Variables
                  - Set: AZURE_CLIENT_ID, AZURE_TENANT_ID, AZURE_CLIENT_SECRET
                  - Best for: Service principals and automated scenarios

               4️⃣  Azure PowerShell
                  - Install: https://docs.microsoft.com/powershell/azure/install-az-ps
                  - Login: Run 'Connect-AzAccount'
                  - Best for: PowerShell users

               5️⃣  Shared Token Cache
                  - Automatically used if available from other Azure tools
                  - Best for: Shared credentials across tools

               🔍 Credential Discovery Process:
                  1. Server automatically discovers all available credentials
                  2. If only one credential found → uses it automatically
                  3. If multiple credentials found → prompts you to choose
                  4. Selected credential is used for entire session

               📋 Useful Commands:
                  - List credentials           - See all available credentials
                  - Select credential          - Choose a specific credential
                  - Get current credential     - See which credential is active
                  - Clear credential selection - Force re-discovery

               💡 Troubleshooting:
                  - No credentials found? → Run 'az login' to authenticate
                  - Wrong tenant? → Use azure:list_credentials to see all options
                  - Multiple accounts? → Use azure:select_credential to switch
                  - Need to refresh? → Use azure:clear_credential_selection

               For more information: https://learn.microsoft.com/azure/developer/intro/azure-developer-authentication
               """;
    }
}