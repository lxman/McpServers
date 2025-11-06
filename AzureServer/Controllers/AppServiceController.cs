using AzureServer.Core.Services.AppService;
using AzureServer.Core.Services.AppService.Models;
using Microsoft.AspNetCore.Mvc;

namespace AzureServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AppServiceController(IAppServiceService appServiceService, ILogger<AppServiceController> logger) : ControllerBase
{
    [HttpGet("webapps")]
    public async Task<ActionResult> ListWebApps([FromQuery] string? subscriptionId = null, [FromQuery] string? resourceGroupName = null)
    {
        try
        {
            IEnumerable<WebAppDto> webApps = await appServiceService.ListWebAppsAsync(subscriptionId, resourceGroupName);
            return Ok(new { success = true, webApps = webApps.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing web apps");
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ListWebApps", type = ex.GetType().Name });
        }
    }

    [HttpGet("webapps/{resourceGroupName}/{webAppName}")]
    public async Task<ActionResult> GetWebApp(string webAppName, string resourceGroupName, [FromQuery] string? subscriptionId = null)
    {
        try
        {
            WebAppDto? webApp = await appServiceService.GetWebAppAsync(webAppName, resourceGroupName, subscriptionId);
            if (webApp is null)
                return NotFound(new { success = false, error = $"Web app {webAppName} not found" });

            return Ok(new { success = true, webApp });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting web app {WebAppName}", webAppName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetWebApp", type = ex.GetType().Name });
        }
    }

    [HttpPost("webapps/{resourceGroupName}/{webAppName}/start")]
    public async Task<ActionResult> StartWebApp(string webAppName, string resourceGroupName, [FromQuery] string? subscriptionId = null)
    {
        try
        {
            bool success = await appServiceService.StartWebAppAsync(webAppName, resourceGroupName, subscriptionId);
            return Ok(new { success, message = success ? $"Web app {webAppName} started" : $"Failed to start web app {webAppName}" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error starting web app {WebAppName}", webAppName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "StartWebApp", type = ex.GetType().Name });
        }
    }

    [HttpPost("webapps/{resourceGroupName}/{webAppName}/stop")]
    public async Task<ActionResult> StopWebApp(string webAppName, string resourceGroupName, [FromQuery] string? subscriptionId = null)
    {
        try
        {
            bool success = await appServiceService.StopWebAppAsync(webAppName, resourceGroupName, subscriptionId);
            return Ok(new { success, message = success ? $"Web app {webAppName} stopped" : $"Failed to stop web app {webAppName}" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error stopping web app {WebAppName}", webAppName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "StopWebApp", type = ex.GetType().Name });
        }
    }

    [HttpPost("webapps/{resourceGroupName}/{webAppName}/restart")]
    public async Task<ActionResult> RestartWebApp(string webAppName, string resourceGroupName, [FromQuery] string? subscriptionId = null)
    {
        try
        {
            bool success = await appServiceService.RestartWebAppAsync(webAppName, resourceGroupName, subscriptionId);
            return Ok(new { success, message = success ? $"Web app {webAppName} restarted" : $"Failed to restart web app {webAppName}" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error restarting web app {WebAppName}", webAppName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "RestartWebApp", type = ex.GetType().Name });
        }
    }

    [HttpGet("webapps/{resourceGroupName}/{webAppName}/slots")]
    public async Task<ActionResult> ListDeploymentSlots(string webAppName, string resourceGroupName, [FromQuery] string? subscriptionId = null)
    {
        try
        {
            IEnumerable<DeploymentSlotDto> slots = await appServiceService.ListDeploymentSlotsAsync(webAppName, resourceGroupName, subscriptionId);
            return Ok(new { success = true, slots = slots.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing deployment slots for {WebAppName}", webAppName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ListDeploymentSlots", type = ex.GetType().Name });
        }
    }

    [HttpGet("webapps/{resourceGroupName}/{webAppName}/slots/{slotName}")]
    public async Task<ActionResult> GetDeploymentSlot(string webAppName, string slotName, string resourceGroupName, [FromQuery] string? subscriptionId = null)
    {
        try
        {
            DeploymentSlotDto? slot = await appServiceService.GetDeploymentSlotAsync(webAppName, slotName, resourceGroupName, subscriptionId);
            if (slot is null)
                return NotFound(new { success = false, error = $"Deployment slot {slotName} not found" });

            return Ok(new { success = true, slot });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting deployment slot {SlotName}", slotName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetDeploymentSlot", type = ex.GetType().Name });
        }
    }

    [HttpPost("webapps/{resourceGroupName}/{webAppName}/swap-slots")]
    public async Task<ActionResult> SwapSlots(
        string webAppName,
        string resourceGroupName,
        [FromBody] SwapSlotsRequest request)
    {
        try
        {
            bool success = await appServiceService.SwapSlotsAsync(webAppName, request.SourceSlotName, request.TargetSlotName, resourceGroupName, request.SubscriptionId);
            return Ok(new { success, message = success ? $"Swapped {request.SourceSlotName} -> {request.TargetSlotName}" : "Swap failed" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error swapping slots for {WebAppName}", webAppName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "SwapSlots", type = ex.GetType().Name });
        }
    }

    [HttpPost("webapps/{resourceGroupName}/{webAppName}/slots/{slotName}/start")]
    public async Task<ActionResult> StartSlot(string webAppName, string slotName, string resourceGroupName, [FromQuery] string? subscriptionId = null)
    {
        try
        {
            bool success = await appServiceService.StartSlotAsync(webAppName, slotName, resourceGroupName, subscriptionId);
            return Ok(new { success, message = success ? $"Slot {slotName} started" : "Start failed" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error starting slot {SlotName}", slotName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "StartSlot", type = ex.GetType().Name });
        }
    }

    [HttpPost("webapps/{resourceGroupName}/{webAppName}/slots/{slotName}/stop")]
    public async Task<ActionResult> StopSlot(string webAppName, string slotName, string resourceGroupName, [FromQuery] string? subscriptionId = null)
    {
        try
        {
            bool success = await appServiceService.StopSlotAsync(webAppName, slotName, resourceGroupName, subscriptionId);
            return Ok(new { success, message = success ? $"Slot {slotName} stopped" : "Stop failed" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error stopping slot {SlotName}", slotName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "StopSlot", type = ex.GetType().Name });
        }
    }

    [HttpGet("webapps/{resourceGroupName}/{webAppName}/settings")]
    public async Task<ActionResult> GetAppSettings(string webAppName, string resourceGroupName, [FromQuery] string? subscriptionId = null)
    {
        try
        {
            AppSettingsDto settings = await appServiceService.GetAppSettingsAsync(webAppName, resourceGroupName, subscriptionId);
            return Ok(new { success = true, settings = settings.Settings });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting app settings for {WebAppName}", webAppName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetAppSettings", type = ex.GetType().Name });
        }
    }

    [HttpPut("webapps/{resourceGroupName}/{webAppName}/settings")]
    public async Task<ActionResult> UpdateAppSettings(
        string webAppName,
        string resourceGroupName,
        [FromBody] Dictionary<string, string> settings,
        [FromQuery] string? subscriptionId = null)
    {
        try
        {
            bool success = await appServiceService.UpdateAppSettingsAsync(webAppName, resourceGroupName, settings, subscriptionId);
            return Ok(new { success, message = success ? "Settings updated" : "Update failed" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating app settings for {WebAppName}", webAppName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "UpdateAppSettings", type = ex.GetType().Name });
        }
    }

    [HttpGet("webapps/{resourceGroupName}/{webAppName}/connection-strings")]
    public async Task<ActionResult> GetConnectionStrings(string webAppName, string resourceGroupName, [FromQuery] string? subscriptionId = null)
    {
        try
        {
            IEnumerable<ConnectionStringDto> connectionStrings = await appServiceService.GetConnectionStringsAsync(webAppName, resourceGroupName, subscriptionId);
            return Ok(new { success = true, connectionStrings = connectionStrings.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting connection strings for {WebAppName}", webAppName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetConnectionStrings", type = ex.GetType().Name });
        }
    }

    [HttpPost("webapps/{resourceGroupName}/{webAppName}/scale")]
    public async Task<ActionResult> ScaleWebApp(
        string webAppName,
        string resourceGroupName,
        [FromBody] ScaleRequest request)
    {
        try
        {
            if (request.InstanceCount is < 1 or > 30)
                return BadRequest(new { success = false, error = "Instance count must be between 1 and 30" });

            bool success = await appServiceService.ScaleWebAppAsync(webAppName, resourceGroupName, request.InstanceCount, request.SubscriptionId);
            return Ok(new { success, message = success ? $"Scaled to {request.InstanceCount} instances" : "Scaling failed" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error scaling web app {WebAppName}", webAppName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ScaleWebApp", type = ex.GetType().Name });
        }
    }

    [HttpGet("plans")]
    public async Task<ActionResult> ListAppServicePlans([FromQuery] string? subscriptionId = null, [FromQuery] string? resourceGroupName = null)
    {
        try
        {
            IEnumerable<AppServicePlanDto> plans = await appServiceService.ListAppServicePlansAsync(subscriptionId, resourceGroupName);
            return Ok(new { success = true, plans = plans.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing app service plans");
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ListAppServicePlans", type = ex.GetType().Name });
        }
    }

    [HttpGet("plans/{resourceGroupName}/{planName}")]
    public async Task<ActionResult> GetAppServicePlan(string planName, string resourceGroupName, [FromQuery] string? subscriptionId = null)
    {
        try
        {
            AppServicePlanDto? plan = await appServiceService.GetAppServicePlanAsync(planName, resourceGroupName, subscriptionId);
            if (plan is null)
                return NotFound(new { success = false, error = $"App service plan {planName} not found" });

            return Ok(new { success = true, plan });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting app service plan {PlanName}", planName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetAppServicePlan", type = ex.GetType().Name });
        }
    }

    [HttpGet("webapps/{resourceGroupName}/{webAppName}/logs")]
    public async Task<ActionResult> GetApplicationLogs(
        string webAppName,
        string resourceGroupName,
        [FromQuery] int? lastHours = null,
        [FromQuery] string? subscriptionId = null)
    {
        try
        {
            string logs = await appServiceService.GetApplicationLogsAsync(webAppName, resourceGroupName, lastHours, subscriptionId);
            return Ok(new { success = true, logs, note = "Full log streaming requires Kudu API or Azure Monitor integration" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting logs for {WebAppName}", webAppName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetApplicationLogs", type = ex.GetType().Name });
        }
    }
}

public record SwapSlotsRequest(string SourceSlotName, string TargetSlotName, string? SubscriptionId = null);
public record ScaleRequest(int InstanceCount, string? SubscriptionId = null);