using System.ComponentModel;
using Edgar.Core.Models;
using Edgar.Core.Services;
using Mcp.ResponseGuard.Extensions;
using Mcp.ResponseGuard.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace EdgarMcp.McpTools;

[McpServerToolType]
public sealed class HoldingsTools(
    EdgarApiClient edgarClient,
    Filing13FParser parser,
    HoldingsDiffer differ,
    CusipTickerMapper cusipMapper,
    HoldingsStore store,
    OutputGuard outputGuard,
    ILogger<HoldingsTools> logger)
{
    [McpServerTool]
    [DisplayName("get_holdings")]
    [Description("Parse and return all holdings from a specific 13F filing. Fetches the XML informationTable, parses it, resolves CUSIPs to ticker symbols, and caches the result.")]
    public async Task<string> GetHoldings(
        [Description("Name of the tracked filer (e.g. 'Berkshire Hathaway')")] string filerName,
        [Description("Accession number of the filing (from list_filings or get_latest_filing). If omitted, uses the most recent filing.")] string? accessionNumber = null)
    {
        try
        {
            logger.LogDebug("Getting holdings for {Filer}, accession: {Accession}",
                filerName, accessionNumber ?? "latest");

            Filing13F? filing = accessionNumber is not null
                ? (await edgarClient.ListFilingsAsync(filerName, 20))
                    .FirstOrDefault(f => f.AccessionNumber == accessionNumber)
                : await edgarClient.GetLatestFilingAsync(filerName);

            if (filing is null)
                return $"Filing not found for '{filerName}'".ToErrorResponse(outputGuard);

            // Check cached holdings first
            List<Holding>? cached = await store.LoadHoldingsAsync(filerName, filing.ReportDate);
            if (cached is not null)
            {
                logger.LogInformation("Returning cached holdings for {Filer} ({Date})",
                    filerName, filing.ReportDate);
                return new { Filing = filing, Holdings = cached, Source = "cache" }
                    .ToGuardedResponse(outputGuard, "get_holdings");
            }

            // Fetch and parse
            string xml = await edgarClient.FetchInformationTableXmlAsync(filing);
            List<Holding> holdings = parser.Parse(xml);

            // Map CUSIPs to tickers
            await cusipMapper.MapHoldingsAsync(holdings);

            // Cache for future use
            await store.SaveHoldingsAsync(filerName, filing.ReportDate, holdings);

            return new { Filing = filing, Holdings = holdings, Source = "sec.gov" }
                .ToGuardedResponse(outputGuard, "get_holdings");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting holdings for {Filer}", filerName);
            return ex.ToErrorResponse(outputGuard);
        }
    }

    [McpServerTool]
    [DisplayName("diff_holdings")]
    [Description("Compare two quarters' holdings for a filer, showing new positions, increases, decreases, and exits. If accession numbers are omitted, uses the two most recent filings.")]
    public async Task<string> DiffHoldings(
        [Description("Name of the tracked filer (e.g. 'Berkshire Hathaway')")] string filerName,
        [Description("Accession number of the previous (older) filing. If omitted, uses second most recent.")] string? previousAccession = null,
        [Description("Accession number of the current (newer) filing. If omitted, uses most recent.")] string? currentAccession = null)
    {
        try
        {
            logger.LogDebug("Diffing holdings for {Filer}", filerName);

            List<Filing13F> filings = await edgarClient.ListFilingsAsync(filerName, 20);
            if (filings.Count < 2)
                return "Need at least 2 filings to diff. Only found {filings.Count}."
                    .ToErrorResponse(outputGuard);

            Filing13F? currentFiling = currentAccession is not null
                ? filings.FirstOrDefault(f => f.AccessionNumber == currentAccession)
                : filings[0];

            Filing13F? previousFiling = previousAccession is not null
                ? filings.FirstOrDefault(f => f.AccessionNumber == previousAccession)
                : filings[1];

            if (currentFiling is null || previousFiling is null)
                return "Could not find specified filings".ToErrorResponse(outputGuard);

            // Get holdings for both quarters
            List<Holding> currentHoldings = await GetOrFetchHoldings(filerName, currentFiling);
            List<Holding> previousHoldings = await GetOrFetchHoldings(filerName, previousFiling);

            // Diff
            HoldingsDiff diff = differ.Diff(
                previousHoldings, previousFiling.ReportDate,
                currentHoldings, currentFiling.ReportDate,
                filerName);

            // Map tickers on the diff
            await cusipMapper.MapChangesAsync(diff);

            return diff.ToGuardedResponse(outputGuard, "diff_holdings");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error diffing holdings for {Filer}", filerName);
            return ex.ToErrorResponse(outputGuard);
        }
    }

    private async Task<List<Edgar.Core.Models.Holding>> GetOrFetchHoldings(
        string filerName, Edgar.Core.Models.Filing13F filing)
    {
        List<Holding>? cached = await store.LoadHoldingsAsync(filerName, filing.ReportDate);
        if (cached is not null) return cached;

        string xml = await edgarClient.FetchInformationTableXmlAsync(filing);
        List<Holding> holdings = parser.Parse(xml);
        await cusipMapper.MapHoldingsAsync(holdings);
        await store.SaveHoldingsAsync(filerName, filing.ReportDate, holdings);
        return holdings;
    }
}
