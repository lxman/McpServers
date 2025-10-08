using System.ComponentModel;
using AzureMcp.Authentication;
using AzureMcp.Authentication.models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace AzureMcp.Tools;

/// <summary>
/// MCP tools for Azure AD/Entra authentication
/// </summary>
[McpServerToolType]
public class EntraAuthTools(
    ILogger<EntraAuthTools> logger,
    EntraCredentialService entraService)
{
    #region Interactive Browser Authentication

    [McpServerTool][Description("⚠️ INTERACTIVE BROWSER AUTHENTICATION ⚠️\n" +
        "Opens the user's web browser for Azure AD sign-in.\n" +
        "REQUIRED: Ask user for permission first with a question like:\n" +
        "'Would you like me to open a browser window to sign in to Azure?'\n" +
        "Only call this tool if user explicitly agrees.")]
    public async Task<AuthenticationResult> AuthenticateInteractiveAsync(
        [Description("REQUIRED: Must be true. Confirms user gave permission to open browser.")]
        bool userConfirmed,

        [Description("Optional: Azure AD tenant ID. Use 'common' for multi-tenant, 'organizations' for any org, or leave null for default.")]
        string? tenantId = null,

        [Description("Optional: Application client ID. Leave null to use Azure CLI's well-known client ID.")]
        string? clientId = null)
    {
        // Fail-safe check
        if (!userConfirmed)
        {
            logger.LogWarning("Interactive authentication called without user confirmation");
            return new AuthenticationResult
            {
                Success = false,
                ErrorCode = "CONFIRMATION_REQUIRED",
                Message = "⚠️ User confirmation required before opening browser.\n" +
                         "Please ask: 'Would you like me to open a browser window to authenticate with Azure?'\n" +
                         "Call this tool again with userConfirmed=true only if user agrees."
            };
        }

        logger.LogInformation("User confirmed browser authentication - proceeding");
        return await entraService.AuthenticateInteractiveBrowserAsync(tenantId, clientId);
    }

    #endregion

    #region Device Code Flow Authentication

    [McpServerTool][Description("Start device code flow authentication for Azure AD.\n" +
        "This is useful for headless/remote scenarios where a browser cannot be opened.\n" +
        "Returns a code that the user can enter at microsoft.com/devicelogin from any device.\n" +
        "After user enters the code, call entra:authenticate_device_code_complete to finish.")]
    public async Task<DeviceCodeResult> StartDeviceCodeAuthenticationAsync(
        [Description("Optional: Azure AD tenant ID. Use 'common' for multi-tenant or leave null for default.")]
        string? tenantId = null,

        [Description("Optional: Application client ID. Leave null to use Azure CLI's client ID.")]
        string? clientId = null)
    {
        logger.LogInformation("Starting device code authentication flow");
        return await entraService.InitiateDeviceCodeFlowAsync(tenantId, clientId);
    }

    [McpServerTool][Description("Complete device code authentication after user has entered the code.\nCall this after Start device code authentication once user confirms they've entered the code.")]
    public async Task<AuthenticationResult> CompleteDeviceCodeAuthenticationAsync(
        [Description("REQUIRED: Credential ID returned from Start device code authentication")]
        string credentialId)
    {
        logger.LogInformation("Completing device code authentication");
        return await entraService.CompleteDeviceCodeAuthenticationAsync(credentialId);
    }

    #endregion

    #region Service Principal Authentication

    [McpServerTool][Description("Authenticate using Azure AD service principal with client secret.\nThis is for non-interactive authentication using an App Registration.\nYou need: Tenant ID, Client ID (Application ID), and Client Secret.")]
    public async Task<AuthenticationResult> AuthenticateServicePrincipalAsync(
        [Description("REQUIRED: Azure AD tenant ID where the app registration exists")]
        string tenantId,

        [Description("REQUIRED: Application (Client) ID from the app registration")]
        string clientId,

        [Description("REQUIRED: Client secret (password) for the service principal")]
        string clientSecret)
    {
        logger.LogInformation("Authenticating with service principal");
        
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return new AuthenticationResult
            {
                Success = false,
                ErrorCode = "MISSING_TENANT_ID",
                Message = "Tenant ID is required for service principal authentication"
            };
        }

        if (string.IsNullOrWhiteSpace(clientId))
        {
            return new AuthenticationResult
            {
                Success = false,
                ErrorCode = "MISSING_CLIENT_ID",
                Message = "Client ID is required for service principal authentication"
            };
        }

        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            return new AuthenticationResult
            {
                Success = false,
                ErrorCode = "MISSING_CLIENT_SECRET",
                Message = "Client secret is required for service principal authentication"
            };
        }

        return await entraService.AuthenticateClientSecretAsync(tenantId, clientId, clientSecret);
    }

    #endregion

    #region Certificate-based Authentication

    [McpServerTool][Description("Authenticate using Azure AD service principal with certificate.\nThis is more secure than client secret for service principal authentication.\nCertificate can be a .pfx or .p12 file.")]
    public async Task<AuthenticationResult> AuthenticateCertificateAsync(
        [Description("REQUIRED: Azure AD tenant ID where the app registration exists")]
        string tenantId,

        [Description("REQUIRED: Application (Client) ID from the app registration")]
        string clientId,

        [Description("REQUIRED: Full path to certificate file (.pfx or .p12)")]
        string certificatePath,

        [Description("Optional: Password for the certificate file if it's encrypted")]
        string? certificatePassword = null)
    {
        logger.LogInformation("Authenticating with certificate");

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return new AuthenticationResult
            {
                Success = false,
                ErrorCode = "MISSING_TENANT_ID",
                Message = "Tenant ID is required for certificate authentication"
            };
        }

        if (string.IsNullOrWhiteSpace(clientId))
        {
            return new AuthenticationResult
            {
                Success = false,
                ErrorCode = "MISSING_CLIENT_ID",
                Message = "Client ID is required for certificate authentication"
            };
        }

        if (string.IsNullOrWhiteSpace(certificatePath))
        {
            return new AuthenticationResult
            {
                Success = false,
                ErrorCode = "MISSING_CERTIFICATE_PATH",
                Message = "Certificate path is required for certificate authentication"
            };
        }

        if (!File.Exists(certificatePath))
        {
            return new AuthenticationResult
            {
                Success = false,
                ErrorCode = "CERTIFICATE_NOT_FOUND",
                Message = $"Certificate file not found at: {certificatePath}"
            };
        }

        return await entraService.AuthenticateCertificateAsync(
            tenantId, 
            clientId, 
            certificatePath, 
            certificatePassword);
    }

    #endregion

    #region Managed Identity Authentication

    [McpServerTool]
    [Description("Authenticate using Azure Managed Identity.\nOnly works when running on Azure infrastructure (VM, App Service, Container Apps, etc.).\nNo credentials needed - Azure provides identity automatically.")]
    public async Task<AuthenticationResult> AuthenticateManagedIdentityAsync(
        [Description("Optional: Client ID for user-assigned managed identity. Leave null for system-assigned.")]
        string? clientId = null)
    {
        logger.LogInformation("Authenticating with managed identity");
        return await entraService.AuthenticateManagedIdentityAsync(clientId);
    }

    #endregion

    #region Helper Tools

    [McpServerTool]
    [Description("Clear all cached Entra authentication credentials.\nForces re-authentication on next operation.")]
    public Task<string> ClearCredentialCacheAsync()
    {
        logger.LogInformation("Clearing Entra credential cache");
        entraService.ClearCredentialCache();
        return Task.FromResult("✅ Cleared all cached Entra credentials. Next authentication will require re-login.");
    }

    [McpServerTool]
    [Description("Get information about available Entra authentication methods and their requirements.")]
    public static Task<string> GetAuthenticationInfoAsync()
    {
        const string info = """

                            # Azure AD/Entra Authentication Methods

                            ## 1. Interactive Browser (Most Common)
                               - Tool: entra:authenticate_interactive
                               - Opens browser for user sign-in
                               - Requires user confirmation
                               - Tokens cached for future use
                               - Best for: Interactive development scenarios

                            ## 2. Device Code Flow
                               - Tool: entra:authenticate_device_code_start + entra:authenticate_device_code_complete
                               - User enters code at microsoft.com/devicelogin
                               - Good for: Headless/remote scenarios, SSH sessions
                               - No browser popup needed

                            ## 3. Service Principal (Client Secret)
                               - Tool: entra:authenticate_service_principal
                               - Non-interactive authentication
                               - Requires: Tenant ID, Client ID, Client Secret
                               - Best for: Automation, CI/CD, production services

                            ## 4. Certificate-based
                               - Tool: entra:authenticate_certificate
                               - More secure than client secret
                               - Requires: Tenant ID, Client ID, Certificate file (.pfx)
                               - Best for: High-security production scenarios

                            ## 5. Managed Identity
                               - Tool: entra:authenticate_managed_identity
                               - Only works on Azure infrastructure
                               - No credentials needed
                               - Best for: Azure VMs, App Service, Container Apps

                            ## Requirements:
                            - Interactive Browser: Azure CLI's built-in app registration (works everywhere)
                            - Device Code: Azure CLI's built-in app registration (works everywhere)
                            - Service Principal/Certificate: Your own app registration in Azure AD
                            - Managed Identity: Running on Azure infrastructure with MI enabled

                            """;

        return Task.FromResult(info);
    }

    #endregion
}
