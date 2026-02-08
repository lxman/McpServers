using Edgar.Core.Models;
using Microsoft.Extensions.Logging;

namespace Edgar.Core.Services;

public class PortfolioScaler(ILogger<PortfolioScaler> logger)
{
    public List<TradeRecommendation> GenerateRecommendations(
        HoldingsDiff diff,
        decimal accountValue,
        Dictionary<string, decimal>? existingPositions = null)
    {
        var recommendations = new List<TradeRecommendation>();
        existingPositions ??= [];

        // Calculate total portfolio value from the current filing
        decimal totalPortfolioValue = CalculateTotalPortfolioValue(diff);
        if (totalPortfolioValue <= 0)
        {
            logger.LogWarning("Total portfolio value is zero, cannot scale positions");
            return recommendations;
        }

        decimal scaleFactor = accountValue / totalPortfolioValue;
        logger.LogInformation(
            "Scaling factor: {Scale:F10} (Account: {Account:C} / Portfolio: {Portfolio:C})",
            scaleFactor, accountValue, totalPortfolioValue);

        // New positions -> Buy
        foreach (HoldingChange change in diff.NewPositions)
        {
            if (string.IsNullOrEmpty(change.Ticker)) continue;

            decimal targetValue = change.CurrentValue * scaleFactor;
            var weight = (double)(change.CurrentValue / totalPortfolioValue);

            recommendations.Add(new TradeRecommendation
            {
                Ticker = change.Ticker,
                Cusip = change.Cusip,
                NameOfIssuer = change.NameOfIssuer,
                Action = TradeAction.Buy,
                Quantity = Math.Round(targetValue, 2),
                EstimatedValue = targetValue,
                PortfolioWeight = weight,
                Reason = $"New position: {change.NameOfIssuer} ({change.CurrentShares:N0} shares in original portfolio)"
            });
        }

        // Increased positions -> Buy more
        foreach (HoldingChange change in diff.IncreasedPositions)
        {
            if (string.IsNullOrEmpty(change.Ticker)) continue;

            decimal additionalValue = change.ValueDelta * scaleFactor;
            if (additionalValue <= 1) continue; // Skip trivial changes

            recommendations.Add(new TradeRecommendation
            {
                Ticker = change.Ticker,
                Cusip = change.Cusip,
                NameOfIssuer = change.NameOfIssuer,
                Action = TradeAction.Buy,
                Quantity = Math.Round(additionalValue, 2),
                EstimatedValue = additionalValue,
                PortfolioWeight = (double)(change.CurrentValue / totalPortfolioValue),
                Reason = $"Increased position: +{change.SharesDelta:N0} shares ({change.PercentChange:F1}% increase)"
            });
        }

        // Decreased positions -> Sell some
        foreach (HoldingChange change in diff.DecreasedPositions)
        {
            if (string.IsNullOrEmpty(change.Ticker)) continue;

            decimal reduceValue = Math.Abs(change.ValueDelta) * scaleFactor;
            if (reduceValue <= 1) continue;

            recommendations.Add(new TradeRecommendation
            {
                Ticker = change.Ticker,
                Cusip = change.Cusip,
                NameOfIssuer = change.NameOfIssuer,
                Action = TradeAction.Sell,
                Quantity = Math.Round(reduceValue, 2),
                EstimatedValue = reduceValue,
                PortfolioWeight = (double)(change.CurrentValue / totalPortfolioValue),
                Reason = $"Decreased position: {change.SharesDelta:N0} shares ({change.PercentChange:F1}% decrease)"
            });
        }

        // Exited positions -> Sell all
        foreach (HoldingChange change in diff.ExitedPositions)
        {
            if (string.IsNullOrEmpty(change.Ticker)) continue;

            decimal currentHolding = existingPositions.GetValueOrDefault(change.Ticker, 0);
            if (currentHolding <= 0) continue;

            recommendations.Add(new TradeRecommendation
            {
                Ticker = change.Ticker,
                Cusip = change.Cusip,
                NameOfIssuer = change.NameOfIssuer,
                Action = TradeAction.Sell,
                Quantity = currentHolding,
                EstimatedValue = currentHolding,
                PortfolioWeight = 0,
                Reason = $"Exited position: {change.NameOfIssuer} fully sold in original portfolio"
            });
        }

        logger.LogInformation("Generated {Count} trade recommendations", recommendations.Count);
        return recommendations;
    }

    private static decimal CalculateTotalPortfolioValue(HoldingsDiff diff)
    {
        decimal allCurrentValues = diff.NewPositions.Sum(c => c.CurrentValue)
                                   + diff.IncreasedPositions.Sum(c => c.CurrentValue)
                                   + diff.DecreasedPositions.Sum(c => c.CurrentValue)
                                   + diff.UnchangedPositions.Sum(c => c.CurrentValue);

        return allCurrentValues;
    }
}
