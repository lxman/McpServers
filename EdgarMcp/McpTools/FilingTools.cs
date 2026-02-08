using System.ComponentModel;
using Edgar.Core.Models;
using Edgar.Core.Services;
using Mcp.ResponseGuard.Extensions;
using Mcp.ResponseGuard.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace EdgarMcp.McpTools;

[McpServerToolType]
public sealed class FilingTools(
    EdgarApiClient edgarClient,
    HoldingsStore holdingsStore,
    OutputGuard outputGuard,
    ILogger<FilingTools> logger)
{
    [McpServerTool]
    [DisplayName("list_tracked_filers")]
    [Description("Show all configured institutional filers being tracked (e.g. Berkshire Hathaway).")]
    public string ListTrackedFilers()
    {
        try
        {
            List<TrackedFiler> filers = edgarClient.GetTrackedFilers();
            return filers.ToGuardedResponse(outputGuard, "list_tracked_filers");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing tracked filers");
            return ex.ToErrorResponse(outputGuard);
        }
    }

    [McpServerTool]
    [DisplayName("get_latest_filing")]
    [Description("Fetch the most recent 13F filing for a tracked filer. Returns filing metadata including accession number, dates, and form type.")]
    public async Task<string> GetLatestFiling(
        [Description("Name of the tracked filer (e.g. 'Berkshire Hathaway')")] string filerName)
    {
        try
        {
            logger.LogDebug("Getting latest filing for {Filer}", filerName);
            Filing13F? filing = await edgarClient.GetLatestFilingAsync(filerName);

            if (filing is null)
                return $"No 13F filings found for '{filerName}'".ToErrorResponse(outputGuard);

            return filing.ToGuardedResponse(outputGuard, "get_latest_filing");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting latest filing for {Filer}", filerName);
            return ex.ToErrorResponse(outputGuard, "Use list_tracked_filers to see available filers");
        }
    }

    [McpServerTool]
    [DisplayName("check_for_new_filing")]
    [Description("Check if a tracked filer has a newer 13F filing on EDGAR than the most recent one we've synced locally. Returns the new filing details if available, or a message saying we're up to date.")]
    public async Task<string> CheckForNewFiling(
        [Description("Name of the tracked filer (e.g. 'Berkshire Hathaway')")] string filerName)
    {
        try
        {
            logger.LogDebug("Checking for new filing for {Filer}", filerName);

            Filing13F? latest = await edgarClient.GetLatestFilingAsync(filerName);
            if (latest is null)
                return $"No 13F filings found for '{filerName}'".ToErrorResponse(outputGuard);

            List<DateOnly> localDates = holdingsStore.ListAvailableDates(filerName);
            string latestRemoteStr = latest.ReportDate.ToString("yyyy-MM-dd");
            string filingDateStr = latest.FilingDate.ToString("yyyy-MM-dd");

            if (localDates.Count > 0 && localDates[0] >= latest.ReportDate)
            {
                string localDateStr = localDates[0].ToString("yyyy-MM-dd");
                return new
                {
                    newFiling = false,
                    filerName,
                    latestLocalDate = localDateStr,
                    latestRemoteDate = latestRemoteStr,
                    message = $"Up to date. Latest local holdings are for {localDateStr}, latest EDGAR filing is for {latestRemoteStr}."
                }.ToGuardedResponse(outputGuard, "check_for_new_filing");
            }

            string localInfo = localDates.Count > 0
                ? $"Last synced: {localDates[0].ToString("yyyy-MM-dd")}"
                : "No local holdings cached yet";

            return new
            {
                newFiling = true,
                filerName,
                latestLocalDate = localDates.Count > 0 ? localDates[0].ToString("yyyy-MM-dd") : (string?)null,
                latestRemoteDate = latestRemoteStr,
                filingDate = filingDateStr,
                accessionNumber = latest.AccessionNumber,
                message = $"New filing available! {localInfo}. New filing covers {latestRemoteStr} (filed {filingDateStr}). Run sync_portfolio to rebalance."
            }.ToGuardedResponse(outputGuard, "check_for_new_filing");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking for new filing for {Filer}", filerName);
            return ex.ToErrorResponse(outputGuard, "Use list_tracked_filers to see available filers");
        }
    }

    [McpServerTool]
    [DisplayName("list_filings")]
    [Description("List recent 13F filings for a tracked filer with dates and accession numbers.")]
    public async Task<string> ListFilings(
        [Description("Name of the tracked filer (e.g. 'Berkshire Hathaway')")] string filerName,
        [Description("Number of recent filings to return (default: 10)")] int count = 10)
    {
        try
        {
            logger.LogDebug("Listing {Count} filings for {Filer}", count, filerName);
            List<Filing13F> filings = await edgarClient.ListFilingsAsync(filerName, count);
            return filings.ToGuardedResponse(outputGuard, "list_filings");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing filings for {Filer}", filerName);
            return ex.ToErrorResponse(outputGuard, "Use list_tracked_filers to see available filers");
        }
    }
}
