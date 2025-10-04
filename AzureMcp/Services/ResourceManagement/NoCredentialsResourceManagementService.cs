using AzureMcp.Services.ResourceManagement.Models;
using Microsoft.Extensions.Logging;

namespace AzureMcp.Services.ResourceManagement;

/// <summary>
/// Service that provides helpful guidance when no Azure credentials are found
/// </summary>
public class NoCredentialsResourceManagementService(ILogger<ResourceManagementService> logger)
    : IResourceManagementService
{
    private readonly ILogger<ResourceManagementService> _logger = logger;

    public Task<IEnumerable<SubscriptionDto>> GetSubscriptionsAsync()
    {
        throw new InvalidOperationException(
            "No Azure credentials discovered. To fix this:\n" +
            "1. Run: az login\n" +
            "2. Verify login: az account show\n" +
            "3. OR set environment variables: AZURE_CLIENT_ID, AZURE_TENANT_ID, AZURE_CLIENT_SECRET\n" +
            "4. OR use Visual Studio authentication\n\n" +
            "The server will automatically detect credentials from Azure CLI, environment variables, " +
            "Visual Studio, or other Azure credential sources.");
    }

    public Task<SubscriptionDto?> GetSubscriptionAsync(string subscriptionId)
    {
        throw new InvalidOperationException(
            "No Azure credentials discovered. Please run 'az login' and restart the MCP server.");
    }

    public Task<IEnumerable<ResourceGroupDto>> GetResourceGroupsAsync(string? subscriptionId = null)
    {
        throw new InvalidOperationException(
            "No Azure credentials discovered. Please run 'az login' and restart the MCP server.");
    }

    public Task<ResourceGroupDto?> GetResourceGroupAsync(string subscriptionId, string resourceGroupName)
    {
        throw new InvalidOperationException(
            "No Azure credentials discovered. Please run 'az login' and restart the MCP server.");
    }

    public Task<IEnumerable<GenericResourceDto>> GetResourcesAsync(string? subscriptionId = null, string? resourceGroupName = null)
    {
        throw new InvalidOperationException(
            "No Azure credentials discovered. Please run 'az login' and restart the MCP server.");
    }

    public Task<IEnumerable<GenericResourceDto>> GetResourcesByTypeAsync(string resourceType, string? subscriptionId = null)
    {
        throw new InvalidOperationException(
            "No Azure credentials discovered. Please run 'az login' and restart the MCP server.");
    }

    public Task<GenericResourceDto?> GetResourceAsync(string resourceId)
    {
        throw new InvalidOperationException(
            "No Azure credentials discovered. Please run 'az login' and restart the MCP server.");
    }

    public Task<Dictionary<string, int>> GetResourceCountByTypeAsync(string? subscriptionId = null)
    {
        throw new InvalidOperationException(
            "No Azure credentials discovered. Please run 'az login' and restart the MCP server.");
    }
}
