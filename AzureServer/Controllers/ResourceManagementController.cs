using AzureServer.Services.ResourceManagement;
using AzureServer.Services.ResourceManagement.Models;
using Microsoft.AspNetCore.Mvc;

namespace AzureServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ResourceManagementController(IResourceManagementService resourceService, ILogger<ResourceManagementController> logger) : ControllerBase
{
    [HttpGet("subscriptions")]
    public async Task<ActionResult> ListSubscriptions()
    {
        try
        {
            IEnumerable<SubscriptionDto> subscriptions = await resourceService.GetSubscriptionsAsync();
            return Ok(new { success = true, subscriptions = subscriptions.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing subscriptions");
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ListSubscriptions", type = ex.GetType().Name });
        }
    }

    [HttpGet("subscriptions/{subscriptionId}")]
    public async Task<ActionResult> GetSubscription(string subscriptionId)
    {
        try
        {
            SubscriptionDto? subscription = await resourceService.GetSubscriptionAsync(subscriptionId);
            if (subscription is null)
                return NotFound(new { success = false, error = $"Subscription {subscriptionId} not found" });

            return Ok(new { success = true, subscription });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting subscription {SubscriptionId}", subscriptionId);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetSubscription", type = ex.GetType().Name });
        }
    }

    [HttpGet("resource-groups")]
    public async Task<ActionResult> ListResourceGroups([FromQuery] string? subscriptionId = null)
    {
        try
        {
            IEnumerable<ResourceGroupDto> resourceGroups = await resourceService.GetResourceGroupsAsync(subscriptionId);
            return Ok(new { success = true, resourceGroups = resourceGroups.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing resource groups");
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ListResourceGroups", type = ex.GetType().Name });
        }
    }

    [HttpGet("subscriptions/{subscriptionId}/resource-groups/{resourceGroupName}")]
    public async Task<ActionResult> GetResourceGroup(string subscriptionId, string resourceGroupName)
    {
        try
        {
            ResourceGroupDto? resourceGroup = await resourceService.GetResourceGroupAsync(subscriptionId, resourceGroupName);
            if (resourceGroup is null)
                return NotFound(new { success = false, error = $"Resource group {resourceGroupName} not found" });

            return Ok(new { success = true, resourceGroup });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting resource group {ResourceGroupName}", resourceGroupName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetResourceGroup", type = ex.GetType().Name });
        }
    }

    [HttpGet("resources")]
    public async Task<ActionResult> ListResources(
        [FromQuery] string? subscriptionId = null,
        [FromQuery] string? resourceGroupName = null)
    {
        try
        {
            List<GenericResourceDto> resources = (await resourceService.GetResourcesAsync(subscriptionId, resourceGroupName)).ToList();
            return Ok(new { success = true, resources = resources.ToArray(), count = resources.Count });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing resources");
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ListResources", type = ex.GetType().Name });
        }
    }

    [HttpGet("resources/by-type/{resourceType}")]
    public async Task<ActionResult> ListResourcesByType(
        string resourceType,
        [FromQuery] string? subscriptionId = null)
    {
        try
        {
            List<GenericResourceDto> resources = (await resourceService.GetResourcesByTypeAsync(resourceType, subscriptionId)).ToList();
            return Ok(new { success = true, resourceType, resources = resources.ToArray(), count = resources.Count });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing resources by type {ResourceType}", resourceType);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ListResourcesByType", type = ex.GetType().Name });
        }
    }

    [HttpPost("resources/get-by-id")]
    public async Task<ActionResult> GetResource([FromBody] GetResourceRequest request)
    {
        try
        {
            GenericResourceDto? resource = await resourceService.GetResourceAsync(request.ResourceId);
            if (resource is null)
                return NotFound(new { success = false, error = $"Resource {request.ResourceId} not found" });

            return Ok(new { success = true, resource });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting resource {ResourceId}", request.ResourceId);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetResource", type = ex.GetType().Name });
        }
    }

    [HttpGet("resources/count-by-type")]
    public async Task<ActionResult> GetResourceCountByType([FromQuery] string? subscriptionId = null)
    {
        try
        {
            Dictionary<string, int> countByType = await resourceService.GetResourceCountByTypeAsync(subscriptionId);
            var sortedCounts = countByType
                .OrderByDescending(kvp => kvp.Value)
                .Select(kvp => new { resourceType = kvp.Key, count = kvp.Value })
                .ToArray();

            return Ok(new { success = true, totalTypes = sortedCounts.Length, resourceCounts = sortedCounts });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting resource count by type");
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetResourceCountByType", type = ex.GetType().Name });
        }
    }
}

public record GetResourceRequest(string ResourceId);