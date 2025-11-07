using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SeleniumChrome.Core.Models;
using SeleniumChrome.Core.Services;

namespace SeleniumMcp.McpTools;

/// <summary>
/// MCP tools for email alert operations
/// </summary>
[McpServerToolType]
public class EmailAlertTools(
    EmailJobAlertService emailService,
    ILogger<EmailAlertTools> logger)
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    [McpServerTool, DisplayName("get_email_alert_summary")]
    [Description("See skills/selenium/email/get_email_alert_summary.md only when using this tool")]
    public async Task<string> get_email_alert_summary(int daysBack = 7)
    {
        try
        {
            logger.LogDebug("Retrieving email alert summary for last {DaysBack} days", daysBack);

            EmailJobAlertSummary result = await emailService.GetJobAlertSummaryAsync(daysBack);

            return JsonSerializer.Serialize(new
            {
                success = true,
                daysBack,
                summary = result
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving email alert summary");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_email_alert_jobs")]
    [Description("See skills/selenium/email/get_email_alert_jobs.md only when using this tool")]
    public async Task<string> get_email_alert_jobs(
        int daysBack = 7,
        string? source = null)
    {
        try
        {
            logger.LogDebug("Retrieving email alert jobs for last {DaysBack} days from source {Source}", daysBack, source);

            object result;
            if (!string.IsNullOrEmpty(source) && source.Equals("LinkedIn", StringComparison.OrdinalIgnoreCase))
            {
                result = await emailService.GetLinkedInJobAlertsAsync(daysBack);
            }
            else if (!string.IsNullOrEmpty(source) && source.Equals("Glassdoor", StringComparison.OrdinalIgnoreCase))
            {
                result = await emailService.GetGlassdoorJobAlertsAsync(daysBack);
            }
            else
            {
                result = await emailService.GetJobAlertsAsync(daysBack);
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                daysBack,
                source,
                jobs = result
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving email alert jobs for source {Source}", source);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_recent_email_alerts")]
    [Description("See skills/selenium/email/get_recent_email_alerts.md only when using this tool")]
    public async Task<string> get_recent_email_alerts()
    {
        try
        {
            logger.LogDebug("Retrieving recent email alerts");

            List<EnhancedJobListing> result = await emailService.GetRecentJobAlertsAsync();

            return JsonSerializer.Serialize(new
            {
                success = true,
                alerts = result
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving recent email alerts");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_enhanced_email_alerts")]
    [Description("See skills/selenium/email/get_enhanced_email_alerts.md only when using this tool")]
    public async Task<string> get_enhanced_email_alerts(int daysBack = 7)
    {
        try
        {
            logger.LogDebug("Retrieving enhanced email alerts for last {DaysBack} days", daysBack);

            List<EnhancedJobListing> jobs = await emailService.GetJobAlertsAsync(daysBack);
            List<EnhancedJobListing> enhancedJobs = await emailService.EnhanceJobsWithDetails(jobs);

            return JsonSerializer.Serialize(new
            {
                success = true,
                daysBack,
                enhancedJobs
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving enhanced email alerts");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }
}
