using System.Security.Cryptography.X509Certificates;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using AzureMcp.Authentication.models;
using Microsoft.Extensions.Logging;

namespace AzureMcp.Authentication;

/// <summary>
/// Service for Azure AD/Entra authentication flows
/// Handles interactive browser, device code, service principal, certificate, and managed identity
/// </summary>
public class EntraCredentialService(
    ILogger<EntraCredentialService> logger,
    EntraAuthConfig config)
{
    // Cache for created credentials to avoid recreating them
    private readonly Dictionary<string, TokenCredential> _credentialCache = new();

    #region Interactive Browser Authentication

    /// <summary>
    /// Authenticate using interactive browser flow
    /// Opens the user's default browser for Azure AD sign-in
    /// </summary>
    public async Task<AuthenticationResult> AuthenticateInteractiveBrowserAsync(
        string? tenantId = null,
        string? clientId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Starting interactive browser authentication");

            var options = new InteractiveBrowserCredentialOptions
            {
                TenantId = tenantId ?? config.GetEffectiveTenantId(),
                ClientId = clientId ?? config.GetEffectiveClientId(),
                RedirectUri = config.GetEffectiveRedirectUri(),
                Retry =
                {
                    MaxRetries = 3,
                    Delay = TimeSpan.FromSeconds(1),
                    MaxDelay = TimeSpan.FromSeconds(config.BrowserTimeoutSeconds)
                }
            };

            if (!string.IsNullOrEmpty(config.AuthorityHost))
            {
                options.AuthorityHost = new Uri(config.AuthorityHost);
            }

            if (config.EnableTokenCache && !string.IsNullOrEmpty(config.TokenCacheName))
            {
                options.TokenCachePersistenceOptions = new TokenCachePersistenceOptions
                {
                    Name = config.TokenCacheName
                };
            }

            var credential = new InteractiveBrowserCredential(options);
            
            // Test the credential and get token info
            var tokenContext = new TokenRequestContext(["https://management.azure.com/.default"]);
            AccessToken token = await credential.GetTokenAsync(tokenContext, cancellationToken);

            // Get account information
            CredentialInfo credentialInfo = await EnrichCredentialInfoAsync(
                credential, 
                "interactive-browser",
                "Interactive Browser");

            // Cache the credential
            var credentialId = $"interactive-browser-{options.TenantId}";
            _credentialCache[credentialId] = credential;

            return new AuthenticationResult
            {
                Success = true,
                AuthMethod = "interactive-browser",
                Message = "Successfully authenticated via browser. Token has been cached for future use.",
                ExpiresOn = token.ExpiresOn,
                AccountName = credentialInfo.AccountName,
                TenantId = credentialInfo.TenantId,
                CredentialId = credentialId,
                Metadata = new Dictionary<string, string>
                {
                    ["ClientId"] = options.ClientId,
                    ["TenantId"] = options.TenantId,
                    ["TokenCached"] = config.EnableTokenCache.ToString()
                }
            };
        }
        catch (AuthenticationFailedException ex)
        {
            logger.LogError(ex, "Interactive browser authentication failed");
            return new AuthenticationResult
            {
                Success = false,
                AuthMethod = "interactive-browser",
                ErrorCode = "AUTH_FAILED",
                Message = $"Authentication failed: {ex.Message}"
            };
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Interactive browser authentication was cancelled");
            return new AuthenticationResult
            {
                Success = false,
                AuthMethod = "interactive-browser",
                ErrorCode = "CANCELLED",
                Message = "Authentication was cancelled by user or timed out"
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during interactive browser authentication");
            return new AuthenticationResult
            {
                Success = false,
                AuthMethod = "interactive-browser",
                ErrorCode = "UNEXPECTED_ERROR",
                Message = $"Unexpected error: {ex.Message}"
            };
        }
    }

    #endregion

    #region Device Code Flow Authentication

    /// <summary>
    /// Initiate device code flow authentication
    /// Returns code for user to enter at microsoft.com/devicelogin
    /// </summary>
    public async Task<DeviceCodeResult> InitiateDeviceCodeFlowAsync(
        string? tenantId = null,
        string? clientId = null)
    {
        try
        {
            logger.LogInformation("Initiating device code flow");

            DeviceCodeInfo? capturedDeviceCode = null;
            var deviceCodeReceived = new TaskCompletionSource<bool>();

            var options = new DeviceCodeCredentialOptions
            {
                TenantId = tenantId ?? config.GetEffectiveTenantId(),
                ClientId = clientId ?? config.GetEffectiveClientId(),
                DeviceCodeCallback = (code, cancellation) =>
                {
                    capturedDeviceCode = code;
                    deviceCodeReceived.TrySetResult(true);
                    return Task.CompletedTask;
                }
            };

            if (!string.IsNullOrEmpty(config.AuthorityHost))
            {
                options.AuthorityHost = new Uri(config.AuthorityHost);
            }

            if (config.EnableTokenCache && !string.IsNullOrEmpty(config.TokenCacheName))
            {
                options.TokenCachePersistenceOptions = new TokenCachePersistenceOptions
                {
                    Name = config.TokenCacheName
                };
            }

            var credential = new DeviceCodeCredential(options);

            // Start the authentication process (this will trigger the callback)
            var tokenContext = new TokenRequestContext(["https://management.azure.com/.default"]);
            
            // Start token acquisition in background to trigger device code generation
            _ = Task.Run(async () =>
            {
                try
                {
                    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(config.DeviceCodeTimeoutSeconds));
                    await credential.GetTokenAsync(tokenContext, cts.Token);
                }
                catch
                {
                    // Expected - user hasn't entered code yet
                }
            });

            // Wait for callback with timeout
            Task timeout = Task.Delay(10000);
            Task completedTask = await Task.WhenAny(deviceCodeReceived.Task, timeout);

            if (completedTask == timeout || capturedDeviceCode is null)
            {
                return new DeviceCodeResult
                {
                    Success = false,
                    ErrorMessage = "Timeout waiting for device code generation."
                };
            }

            // Cache the credential for completion
            var credentialId = $"device-code-{options.TenantId}";
            _credentialCache[credentialId] = credential;

            return new DeviceCodeResult
            {
                Success = true,
                CredentialId = credentialId,
                UserCode = capturedDeviceCode.Value.UserCode,
                VerificationUri = capturedDeviceCode.Value.VerificationUri.ToString(),
                ExpiresOn = capturedDeviceCode.Value.ExpiresOn,
                Message = $"To sign in, use a web browser to open the page {capturedDeviceCode.Value.VerificationUri} " +
                         $"and enter the code {capturedDeviceCode.Value.UserCode} to authenticate."
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initiate device code flow");
            return new DeviceCodeResult
            {
                Success = false,
                ErrorMessage = $"Failed to initiate device code flow: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Complete device code authentication after user enters code
    /// </summary>
    public async Task<AuthenticationResult> CompleteDeviceCodeAuthenticationAsync(
        string credentialId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_credentialCache.TryGetValue(credentialId, out TokenCredential? credential))
            {
                return new AuthenticationResult
                {
                    Success = false,
                    AuthMethod = "device-code",
                    ErrorCode = "CREDENTIAL_NOT_FOUND",
                    Message = "Device code session not found. Please initiate device code flow again."
                };
            }

            // Wait for user to complete authentication
            var tokenContext = new TokenRequestContext(["https://management.azure.com/.default"]);
            AccessToken token = await credential.GetTokenAsync(tokenContext, cancellationToken);

            // Get account information
            CredentialInfo credentialInfo = await EnrichCredentialInfoAsync(
                credential,
                "device-code", 
                "Device Code Flow");

            return new AuthenticationResult
            {
                Success = true,
                AuthMethod = "device-code",
                Message = "Successfully authenticated via device code. Token has been cached for future use.",
                ExpiresOn = token.ExpiresOn,
                AccountName = credentialInfo.AccountName,
                TenantId = credentialInfo.TenantId,
                CredentialId = credentialId
            };
        }
        catch (AuthenticationFailedException ex)
        {
            logger.LogError(ex, "Device code authentication failed");
            return new AuthenticationResult
            {
                Success = false,
                AuthMethod = "device-code",
                ErrorCode = "AUTH_FAILED",
                Message = $"Authentication failed: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error completing device code authentication");
            return new AuthenticationResult
            {
                Success = false,
                AuthMethod = "device-code",
                ErrorCode = "UNEXPECTED_ERROR",
                Message = $"Unexpected error: {ex.Message}"
            };
        }
    }

    #endregion

    #region Service Principal (Client Credentials) Authentication

    /// <summary>
    /// Authenticate using service principal with client secret
    /// </summary>
    public async Task<AuthenticationResult> AuthenticateClientSecretAsync(
        string tenantId,
        string clientId,
        string clientSecret,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Authenticating with client secret");

            var options = new ClientSecretCredentialOptions();
            
            if (!string.IsNullOrEmpty(config.AuthorityHost))
            {
                options.AuthorityHost = new Uri(config.AuthorityHost);
            }

            var credential = new ClientSecretCredential(tenantId, clientId, clientSecret, options);

            // Test the credential
            var tokenContext = new TokenRequestContext(["https://management.azure.com/.default"]);
            AccessToken token = await credential.GetTokenAsync(tokenContext, cancellationToken);

            // Get service principal information
            CredentialInfo credentialInfo = await EnrichCredentialInfoAsync(
                credential,
                "client-secret",
                "Service Principal (Client Secret)");

            // Cache the credential
            var credentialId = $"client-secret-{tenantId}-{clientId}";
            _credentialCache[credentialId] = credential;

            return new AuthenticationResult
            {
                Success = true,
                AuthMethod = "client-secret",
                Message = "Successfully authenticated with service principal using client secret.",
                ExpiresOn = token.ExpiresOn,
                TenantId = credentialInfo.TenantId,
                CredentialId = credentialId,
                Metadata = new Dictionary<string, string>
                {
                    ["ClientId"] = clientId,
                    ["TenantId"] = tenantId
                }
            };
        }
        catch (AuthenticationFailedException ex)
        {
            logger.LogError(ex, "Client secret authentication failed");
            return new AuthenticationResult
            {
                Success = false,
                AuthMethod = "client-secret",
                ErrorCode = "AUTH_FAILED",
                Message = $"Authentication failed: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during client secret authentication");
            return new AuthenticationResult
            {
                Success = false,
                AuthMethod = "client-secret",
                ErrorCode = "UNEXPECTED_ERROR",
                Message = $"Unexpected error: {ex.Message}"
            };
        }
    }

    #endregion

    #region Certificate-based Authentication

    /// <summary>
    /// Authenticate using service principal with certificate
    /// </summary>
    public async Task<AuthenticationResult> AuthenticateCertificateAsync(
        string tenantId,
        string clientId,
        string certificatePath,
        string? certificatePassword = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Authenticating with certificate from {Path}", certificatePath);

            // Load certificate
            X509Certificate2 certificate;
            if (!string.IsNullOrEmpty(certificatePassword))
            {
                certificate = X509CertificateLoader.LoadPkcs12FromFile(certificatePath, certificatePassword);
            }
            else
            {
                certificate = X509CertificateLoader.LoadCertificateFromFile(certificatePath);
            }

            var options = new ClientCertificateCredentialOptions();
            
            if (!string.IsNullOrEmpty(config.AuthorityHost))
            {
                options.AuthorityHost = new Uri(config.AuthorityHost);
            }

            var credential = new ClientCertificateCredential(tenantId, clientId, certificate, options);

            // Test the credential
            var tokenContext = new TokenRequestContext(["https://management.azure.com/.default"]);
            AccessToken token = await credential.GetTokenAsync(tokenContext, cancellationToken);

            // Get service principal information
            CredentialInfo credentialInfo = await EnrichCredentialInfoAsync(
                credential,
                "certificate",
                "Service Principal (Certificate)");

            // Cache the credential
            var credentialId = $"certificate-{tenantId}-{clientId}";
            _credentialCache[credentialId] = credential;

            return new AuthenticationResult
            {
                Success = true,
                AuthMethod = "certificate",
                Message = "Successfully authenticated with service principal using certificate.",
                ExpiresOn = token.ExpiresOn,
                TenantId = credentialInfo.TenantId,
                CredentialId = credentialId,
                Metadata = new Dictionary<string, string>
                {
                    ["ClientId"] = clientId,
                    ["TenantId"] = tenantId,
                    ["CertificateThumbprint"] = certificate.Thumbprint
                }
            };
        }
        catch (AuthenticationFailedException ex)
        {
            logger.LogError(ex, "Certificate authentication failed");
            return new AuthenticationResult
            {
                Success = false,
                AuthMethod = "certificate",
                ErrorCode = "AUTH_FAILED",
                Message = $"Authentication failed: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during certificate authentication");
            return new AuthenticationResult
            {
                Success = false,
                AuthMethod = "certificate",
                ErrorCode = "UNEXPECTED_ERROR",
                Message = $"Unexpected error: {ex.Message}"
            };
        }
    }

    #endregion

    #region Managed Identity Authentication

    /// <summary>
    /// Authenticate using managed identity
    /// </summary>
    public async Task<AuthenticationResult> AuthenticateManagedIdentityAsync(
        string? clientId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Authenticating with managed identity");

            var options =
                new ManagedIdentityCredentialOptions(ManagedIdentityId.FromUserAssignedClientId(clientId ?? config.ManagedIdentityClientId));

            var credential = new ManagedIdentityCredential(options);

            // Test the credential
            var tokenContext = new TokenRequestContext(["https://management.azure.com/.default"]);
            AccessToken token = await credential.GetTokenAsync(tokenContext, cancellationToken);

            // Get managed identity information
            CredentialInfo credentialInfo = await EnrichCredentialInfoAsync(
                credential,
                "managed-identity",
                "Managed Identity");

            // Cache the credential
            var credentialId = $"managed-identity-{clientId ?? "system"}";
            _credentialCache[credentialId] = credential;

            return new AuthenticationResult
            {
                Success = true,
                AuthMethod = "managed-identity",
                Message = "Successfully authenticated with managed identity.",
                ExpiresOn = token.ExpiresOn,
                TenantId = credentialInfo.TenantId,
                CredentialId = credentialId,
                Metadata = new Dictionary<string, string>
                {
                    ["IdentityType"] = string.IsNullOrEmpty(clientId) ? "system-assigned" : "user-assigned",
                    ["ClientId"] = clientId ?? "system-assigned"
                }
            };
        }
        catch (AuthenticationFailedException ex)
        {
            logger.LogError(ex, "Managed identity authentication failed");
            return new AuthenticationResult
            {
                Success = false,
                AuthMethod = "managed-identity",
                ErrorCode = "AUTH_FAILED",
                Message = $"Authentication failed: {ex.Message}. Ensure this is running on Azure infrastructure with managed identity enabled."
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during managed identity authentication");
            return new AuthenticationResult
            {
                Success = false,
                AuthMethod = "managed-identity",
                ErrorCode = "UNEXPECTED_ERROR",
                Message = $"Unexpected error: {ex.Message}"
            };
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Enrich credential info by testing it and gathering metadata
    /// </summary>
    private async Task<CredentialInfo> EnrichCredentialInfoAsync(
        TokenCredential credential,
        string authMethod,
        string source)
    {
        var info = new CredentialInfo
        {
            Id = $"{authMethod}-{Guid.NewGuid():N}",
            Source = source,
            AuthenticationMethod = authMethod,
            IsInteractive = authMethod is "interactive-browser" or "device-code",
            SupportsRefresh = true // Most Azure Identity credentials support refresh
        };

        try
        {
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

            // Get subscription information (limit for performance)
            try
            {
                SubscriptionCollection? subscriptions = armClient.GetSubscriptions();
                var count = 0;
                const int maxSubscriptions = 10;
                
                await foreach (SubscriptionResource? subscription in subscriptions)
                {
                    if (count >= maxSubscriptions) break;
                    info.SubscriptionIds.Add(subscription.Data.SubscriptionId ?? string.Empty);
                    
                    if (string.IsNullOrEmpty(info.AccountName) && !string.IsNullOrEmpty(subscription.Data.DisplayName))
                    {
                        info.Metadata["FirstSubscription"] = subscription.Data.DisplayName;
                    }
                    count++;
                }
                
                if (count >= maxSubscriptions)
                {
                    info.Metadata["SubscriptionCount"] = $"Showing first {maxSubscriptions} of potentially more subscriptions";
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug("Could not get subscription info: {Error}", ex.Message);
            }

            info.IsValid = true;
        }
        catch (Exception ex)
        {
            info.IsValid = false;
            info.ErrorMessage = ex.Message;
            logger.LogError(ex, "Failed to enrich credential info");
        }

        return info;
    }

    /// <summary>
    /// Get a cached credential by ID
    /// </summary>
    public TokenCredential? GetCachedCredential(string credentialId)
    {
        return _credentialCache.GetValueOrDefault(credentialId);
    }

    /// <summary>
    /// Clear all cached credentials
    /// </summary>
    public void ClearCredentialCache()
    {
        _credentialCache.Clear();
        logger.LogInformation("Cleared all cached credentials");
    }

    #endregion
}
