using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SeleniumChrome.Core.Models;
using SeleniumChrome.Core.Services.Enhanced;

namespace SeleniumMcp.McpTools;

/// <summary>
/// MCP tools for application tracking operations
/// </summary>
[McpServerToolType]
public class ApplicationTrackingTools(
    ApplicationManagementService applicationService,
    ILogger<ApplicationTrackingTools> logger)
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    [McpServerTool, DisplayName("track_application")]
    [Description("See skills/selenium/tracking/track_application.md only when using this tool")]
    public async Task<string> track_application(string applicationJson)
    {
        try
        {
            logger.LogDebug("Tracking new application");

            ApplicationRecord application = JsonSerializer.Deserialize<ApplicationRecord>(applicationJson)
                                            ?? throw new ArgumentException("Invalid application JSON");

            bool result = await applicationService.TrackApplicationAsync(application);

            return JsonSerializer.Serialize(new
            {
                success = result,
                applicationId = application.Id
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error tracking application");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("update_application_status")]
    [Description("See skills/selenium/tracking/update_application_status.md only when using this tool")]
    public async Task<string> update_application_status(
        string applicationId,
        ApplicationStatus status,
        string? notes = null)
    {
        try
        {
            logger.LogDebug("Updating application {ApplicationId} status to {Status}", applicationId, status);

            bool result = await applicationService.UpdateApplicationStatusAsync(
                applicationId,
                status,
                notes);

            return JsonSerializer.Serialize(new
            {
                success = true,
                applicationId,
                status,
                notes,
                result
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating application {ApplicationId} status", applicationId);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }
}
