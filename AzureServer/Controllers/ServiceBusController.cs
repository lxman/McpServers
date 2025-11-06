using AzureServer.Core.Services.ServiceBus;
using AzureServer.Core.Services.ServiceBus.Models;
using Microsoft.AspNetCore.Mvc;

namespace AzureServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ServiceBusController(IServiceBusService serviceBusService, ILogger<ServiceBusController> logger) : ControllerBase
{
    [HttpGet("namespaces")]
    public async Task<ActionResult> ListNamespaces(
        [FromQuery] string? resourceGroupName = null,
        [FromQuery] string? subscriptionId = null)
    {
        try
        {
            IEnumerable<ServiceBusNamespaceDto> namespaces = await serviceBusService.ListNamespacesAsync(resourceGroupName, subscriptionId);
            return Ok(new { success = true, namespaces = namespaces.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing Service Bus namespaces");
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ListNamespaces", type = ex.GetType().Name });
        }
    }

    [HttpGet("namespaces/{resourceGroupName}/{namespaceName}")]
    public async Task<ActionResult> GetNamespace(
        string resourceGroupName,
        string namespaceName,
        [FromQuery] string? subscriptionId = null)
    {
        try
        {
            ServiceBusNamespaceDto? ns = await serviceBusService.GetNamespaceAsync(resourceGroupName, namespaceName, subscriptionId);
            if (ns is null)
                return NotFound(new { success = false, error = $"Namespace {namespaceName} not found" });

            return Ok(new { success = true, @namespace = ns });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting namespace {NamespaceName}", namespaceName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetNamespace", type = ex.GetType().Name });
        }
    }

    [HttpPost("namespaces/{resourceGroupName}/{namespaceName}")]
    public async Task<ActionResult> CreateNamespace(
        string resourceGroupName,
        string namespaceName,
        [FromBody] CreateNamespaceRequest request)
    {
        try
        {
            ServiceBusNamespaceDto ns = await serviceBusService.CreateNamespaceAsync(
                resourceGroupName, namespaceName, request.Location, request.SubscriptionId, request.Sku);
            return Ok(new { success = true, @namespace = ns });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating namespace {NamespaceName}", namespaceName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "CreateNamespace", type = ex.GetType().Name });
        }
    }

    [HttpDelete("namespaces/{resourceGroupName}/{namespaceName}")]
    public async Task<ActionResult> DeleteNamespace(
        string resourceGroupName,
        string namespaceName,
        [FromQuery] string? subscriptionId = null)
    {
        try
        {
            await serviceBusService.DeleteNamespaceAsync(resourceGroupName, namespaceName, subscriptionId);
            return Ok(new { success = true, message = $"Namespace {namespaceName} deleted successfully" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting namespace {NamespaceName}", namespaceName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "DeleteNamespace", type = ex.GetType().Name });
        }
    }

    [HttpGet("namespaces/{resourceGroupName}/{namespaceName}/queues")]
    public async Task<ActionResult> ListQueues(
        string resourceGroupName,
        string namespaceName,
        [FromQuery] string? subscriptionId = null)
    {
        try
        {
            IEnumerable<ServiceBusQueueDto> queues = await serviceBusService.ListQueuesAsync(resourceGroupName, namespaceName, subscriptionId);
            return Ok(new { success = true, queues = queues.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing queues in namespace {NamespaceName}", namespaceName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ListQueues", type = ex.GetType().Name });
        }
    }
}

public record CreateNamespaceRequest(string Location, string? SubscriptionId = null, string? Sku = "Standard");