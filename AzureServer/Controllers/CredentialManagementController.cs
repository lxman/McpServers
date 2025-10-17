using Azure.Core;
using AzureServer.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace AzureServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CredentialManagementController(CredentialSelectionService selectionService, ILogger<CredentialManagementController> logger) : ControllerBase
{
    [HttpGet("list")]
    public async Task<ActionResult> ListCredentials()
    {
        try
        {
            (_, var result) = await selectionService.GetCredentialAsync();

            if (result.Status == SelectionStatus.NoCredentialsFound)
            {
                return Ok(new
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
                });
            }

            var credentials =
                result.AvailableCredentials
                ?? (result.SelectedCredential is not null ? [result.SelectedCredential] : []);

            if (credentials.Count == 0)
            {
                return Ok(new { success = false, message = "No credentials available" });
            }

            var selected = selectionService.GetSelectedCredential();
            return Ok(new
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
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing credentials");
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ListCredentials", type = ex.GetType().Name });
        }
    }

    [HttpPost("select")]
    public ActionResult SelectCredential([FromBody] SelectCredentialRequest request)
    {
        try
        {
            (var credential, var result) = selectionService.SelectCredential(request.CredentialId);

            if (result.Status == SelectionStatus.Error)
            {
                return BadRequest(new { success = false, error = result.ErrorMessage });
            }

            if (result.Status != SelectionStatus.Selected || result.SelectedCredential is null)
                return BadRequest(new { success = false, error = "Failed to select credential" });

            var info = result.SelectedCredential;
            return Ok(new
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
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error selecting credential");
            return StatusCode(500, new { success = false, error = ex.Message, operation = "SelectCredential", type = ex.GetType().Name });
        }
    }

    [HttpGet("current")]
    public ActionResult GetCurrentCredential()
    {
        try
        {
            var selected = selectionService.GetSelectedCredential();

            if (selected is null)
            {
                return Ok(new
                {
                    success = false,
                    message = "No credential selected",
                    instructions = "Use /api/credentialmanagement/list to see available credentials, then /api/credentialmanagement/select to choose one"
                });
            }

            return Ok(new
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
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting current credential");
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetCurrentCredential", type = ex.GetType().Name });
        }
    }

    [HttpPost("clear")]
    public ActionResult ClearCredentialSelection()
    {
        try
        {
            selectionService.ClearSelection();
            return Ok(new
            {
                success = true,
                message = "Credential selection cleared",
                note = "The next Azure operation will discover and select credentials again"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error clearing credential selection");
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ClearCredentialSelection", type = ex.GetType().Name });
        }
    }

    [HttpGet("help")]
    public ActionResult GetCredentialsHelp()
    {
        return Ok(new
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
                new { endpoint = "/api/credentialmanagement/list", description = "List all available credentials" },
                new { endpoint = "/api/credentialmanagement/select", description = "Select a specific credential" },
                new { endpoint = "/api/credentialmanagement/current", description = "Get currently selected credential" },
                new { endpoint = "/api/credentialmanagement/clear", description = "Clear credential selection" }
            },
            learnMore = "https://learn.microsoft.com/azure/developer/intro/azure-developer-authentication"
        });
    }
}

public record SelectCredentialRequest(string CredentialId);