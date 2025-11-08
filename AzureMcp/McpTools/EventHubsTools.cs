using System.ComponentModel;
using System.Text.Json;
using AzureServer.Core.Services.EventHubs;
using AzureServer.Core.Services.EventHubs.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace AzureMcp.McpTools;

/// <summary>
/// MCP tools for Azure Event Hubs operations
/// </summary>
[McpServerToolType]
public class EventHubsTools(
    IEventHubsService eventHubsService,
    ILogger<EventHubsTools> logger)
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    [McpServerTool, DisplayName("list_eventhubs_namespaces")]
    [Description("List Event Hubs namespaces. See skills/azure/eventhubs/list-namespaces.md only when using this tool")]
    public async Task<string> ListNamespaces(string? resourceGroupName = null, string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Listing Event Hubs namespaces");
            var namespaces = await eventHubsService.ListNamespacesAsync(resourceGroupName, subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                namespaces = namespaces.ToArray()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing Event Hubs namespaces");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "ListNamespaces",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_eventhubs_namespace")]
    [Description("Get Event Hubs namespace details. See skills/azure/eventhubs/get-namespace.md only when using this tool")]
    public async Task<string> GetNamespace(
        string resourceGroupName,
        string namespaceName,
        string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Getting namespace {NamespaceName}", namespaceName);
            var ns = await eventHubsService.GetNamespaceAsync(resourceGroupName, namespaceName, subscriptionId);

            if (ns is null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Namespace {namespaceName} not found"
                }, _jsonOptions);
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                @namespace = ns
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting namespace {NamespaceName}", namespaceName);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "GetNamespace",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("create_eventhubs_namespace")]
    [Description("Create Event Hubs namespace. See skills/azure/eventhubs/create-namespace.md only when using this tool")]
    public async Task<string> CreateNamespace(
        string resourceGroupName,
        string namespaceName,
        string location,
        string? subscriptionId = null,
        string? sku = "Standard")
    {
        try
        {
            logger.LogDebug("Creating namespace {NamespaceName}", namespaceName);

            var ns = await eventHubsService.CreateNamespaceAsync(
                resourceGroupName, namespaceName, location, subscriptionId, sku);

            return JsonSerializer.Serialize(new
            {
                success = true,
                @namespace = ns
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating namespace {NamespaceName}", namespaceName);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "CreateNamespace",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("delete_eventhubs_namespace")]
    [Description("Delete Event Hubs namespace. See skills/azure/eventhubs/delete-namespace.md only when using this tool")]
    public async Task<string> DeleteNamespace(
        string resourceGroupName,
        string namespaceName,
        string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Deleting namespace {NamespaceName}", namespaceName);
            await eventHubsService.DeleteNamespaceAsync(resourceGroupName, namespaceName, subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = $"Namespace {namespaceName} deleted successfully"
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting namespace {NamespaceName}", namespaceName);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "DeleteNamespace",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }
}
