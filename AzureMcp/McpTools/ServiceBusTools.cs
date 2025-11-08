using System.ComponentModel;
using System.Text.Json;
using AzureServer.Core.Services.ServiceBus;
using AzureServer.Core.Services.ServiceBus.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace AzureMcp.McpTools;

/// <summary>
/// MCP tools for Azure Service Bus operations
/// </summary>
[McpServerToolType]
public class ServiceBusTools(
    IServiceBusService serviceBusService,
    ILogger<ServiceBusTools> logger)
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    [McpServerTool, DisplayName("list_servicebus_namespaces")]
    [Description("List Service Bus namespaces. See skills/azure/servicebus/list-namespaces.md only when using this tool")]
    public async Task<string> ListNamespaces(string? resourceGroupName = null, string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Listing Service Bus namespaces");
            var namespaces = await serviceBusService.ListNamespacesAsync(resourceGroupName, subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                namespaces = namespaces.ToArray()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing Service Bus namespaces");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "ListNamespaces",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_servicebus_namespace")]
    [Description("Get Service Bus namespace details. See skills/azure/servicebus/get-namespace.md only when using this tool")]
    public async Task<string> GetNamespace(
        string resourceGroupName,
        string namespaceName,
        string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Getting namespace {NamespaceName}", namespaceName);
            var ns = await serviceBusService.GetNamespaceAsync(resourceGroupName, namespaceName, subscriptionId);

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

    [McpServerTool, DisplayName("create_servicebus_namespace")]
    [Description("Create Service Bus namespace. See skills/azure/servicebus/create-namespace.md only when using this tool")]
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

            var ns = await serviceBusService.CreateNamespaceAsync(
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

    [McpServerTool, DisplayName("delete_servicebus_namespace")]
    [Description("Delete Service Bus namespace. See skills/azure/servicebus/delete-namespace.md only when using this tool")]
    public async Task<string> DeleteNamespace(
        string resourceGroupName,
        string namespaceName,
        string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Deleting namespace {NamespaceName}", namespaceName);
            await serviceBusService.DeleteNamespaceAsync(resourceGroupName, namespaceName, subscriptionId);

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

    [McpServerTool, DisplayName("list_servicebus_queues")]
    [Description("List queues in Service Bus namespace. See skills/azure/servicebus/list-queues.md only when using this tool")]
    public async Task<string> ListQueues(
        string resourceGroupName,
        string namespaceName,
        string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Listing queues in namespace {NamespaceName}", namespaceName);
            var queues = await serviceBusService.ListQueuesAsync(resourceGroupName, namespaceName, subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                queues = queues.ToArray()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing queues in namespace {NamespaceName}", namespaceName);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "ListQueues",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }
}
