using System.ComponentModel;
using Edgar.Core.Models;
using Edgar.Core.Services;
using Mcp.ResponseGuard.Extensions;
using Mcp.ResponseGuard.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace EdgarMcp.McpTools;

[McpServerToolType]
public sealed class TradeTools(
    EdgarApiClient edgarClient,
    Filing13FParser parser,
    HoldingsDiffer differ,
    CusipTickerMapper cusipMapper,
    PortfolioScaler scaler,
    TradeExecutor executor,
    HoldingsStore store,
    OutputGuard outputGuard,
    ILogger<TradeTools> logger)
{
    [McpServerTool]
    [DisplayName("generate_trades")]
    [Description("Generate trade recommendations based on a filer's latest holdings diff, scaled to the Alpaca account size. Does NOT execute trades - use sync_portfolio for that.")]
    public async Task<string> GenerateTrades(
        [Description("Name of the tracked filer (e.g. 'Berkshire Hathaway')")] string filerName)
    {
        try
        {
            logger.LogDebug("Generating trade recommendations for {Filer}", filerName);

            // Get the diff
            List<Filing13F> filings = await edgarClient.ListFilingsAsync(filerName, 20);
            if (filings.Count < 2)
                return "Need at least 2 filings to generate trades".ToErrorResponse(outputGuard);

            Filing13F currentFiling = filings[0];
            Filing13F previousFiling = filings[1];

            List<Holding> currentHoldings = await GetOrFetchHoldings(filerName, currentFiling);
            List<Holding> previousHoldings = await GetOrFetchHoldings(filerName, previousFiling);

            HoldingsDiff diff = differ.Diff(
                previousHoldings, previousFiling.ReportDate,
                currentHoldings, currentFiling.ReportDate,
                filerName);

            await cusipMapper.MapChangesAsync(diff);

            // Get account info
            decimal accountValue = await executor.GetAccountValueAsync();
            Dictionary<string, decimal> existingPositions = await executor.GetExistingPositionsAsync();

            // Generate recommendations
            List<TradeRecommendation> recommendations = scaler.GenerateRecommendations(diff, accountValue, existingPositions);

            return new
            {
                FilerName = filerName,
                PreviousReportDate = previousFiling.ReportDate,
                CurrentReportDate = currentFiling.ReportDate,
                AccountValue = accountValue,
                TotalChanges = diff.TotalChanges,
                Recommendations = recommendations
            }.ToGuardedResponse(outputGuard, "generate_trades");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating trades for {Filer}", filerName);
            return ex.ToErrorResponse(outputGuard);
        }
    }

    [McpServerTool]
    [DisplayName("sync_portfolio")]
    [Description("One-shot operation: fetches latest 13F diff, generates scaled trade recommendations, and executes them on the Alpaca paper trading account.")]
    public async Task<string> SyncPortfolio(
        [Description("Name of the tracked filer (e.g. 'Berkshire Hathaway')")] string filerName,
        [Description("Set to true to actually execute trades. If false, returns recommendations only (dry run).")] bool execute = false)
    {
        try
        {
            logger.LogInformation("Syncing portfolio for {Filer} (execute: {Execute})",
                filerName, execute);

            // Step 1-2: Get the two most recent filings
            List<Filing13F> filings = await edgarClient.ListFilingsAsync(filerName, 20);
            if (filings.Count < 2)
                return "Need at least 2 filings to sync portfolio".ToErrorResponse(outputGuard);

            Filing13F currentFiling = filings[0];
            Filing13F previousFiling = filings[1];

            // Step 3: Parse holdings
            List<Holding> currentHoldings = await GetOrFetchHoldings(filerName, currentFiling);
            List<Holding> previousHoldings = await GetOrFetchHoldings(filerName, previousFiling);

            // Step 4: Diff
            HoldingsDiff diff = differ.Diff(
                previousHoldings, previousFiling.ReportDate,
                currentHoldings, currentFiling.ReportDate,
                filerName);

            // Step 5: Map CUSIPs to tickers
            await cusipMapper.MapChangesAsync(diff);

            // Step 6-7: Get account info and scale
            decimal accountValue = await executor.GetAccountValueAsync();
            Dictionary<string, decimal> existingPositions = await executor.GetExistingPositionsAsync();
            List<TradeRecommendation> recommendations = scaler.GenerateRecommendations(diff, accountValue, existingPositions);

            var result = new Edgar.Core.Models.SyncResult
            {
                FilerName = filerName,
                PreviousReportDate = previousFiling.ReportDate,
                CurrentReportDate = currentFiling.ReportDate,
                TotalChangesDetected = diff.TotalChanges,
                TradesGenerated = recommendations.Count,
                AccountValueUsed = accountValue
            };

            // Check market status and warn if closed
            MarketStatus marketStatus = await executor.GetMarketStatusAsync();
            if (!marketStatus.IsOpen)
            {
                result.Warning = $"Market is currently closed. Next open: {marketStatus.NextOpen:yyyy-MM-dd HH:mm} UTC. " +
                                 "Orders will be queued as DAY orders and submitted at the next market open.";
                logger.LogWarning("Market is closed. Next open: {NextOpen}", marketStatus.NextOpen);
            }

            if (execute && recommendations.Count > 0)
            {
                // Step 8-9: Execute trades
                List<TradeResult> tradeResults = await executor.ExecuteTradesAsync(recommendations);
                result.Results = tradeResults;
                result.TradesExecuted = tradeResults.Count(r => r.Success);
                result.TradesFailed = tradeResults.Count(r => !r.Success);

                logger.LogInformation(
                    "Sync complete: {Executed} executed, {Failed} failed out of {Total} trades",
                    result.TradesExecuted, result.TradesFailed, recommendations.Count);
            }
            else
            {
                logger.LogInformation("Dry run: {Count} trades would be executed", recommendations.Count);
                return new
                {
                    DryRun = true,
                    result.FilerName,
                    result.PreviousReportDate,
                    result.CurrentReportDate,
                    result.TotalChangesDetected,
                    result.TradesGenerated,
                    result.AccountValueUsed,
                    result.Warning,
                    Recommendations = recommendations
                }.ToGuardedResponse(outputGuard, "sync_portfolio");
            }

            // Step 10: Save current holdings for future diffs
            await store.SaveHoldingsAsync(filerName, currentFiling.ReportDate, currentHoldings);

            return result.ToGuardedResponse(outputGuard, "sync_portfolio");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error syncing portfolio for {Filer}", filerName);
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
