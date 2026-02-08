using Edgar.Core.Models;
using Microsoft.Extensions.Logging;

namespace Edgar.Core.Services;

public class HoldingsDiffer(ILogger<HoldingsDiffer> logger)
{
    public HoldingsDiff Diff(
        List<Holding> previous, DateOnly previousDate,
        List<Holding> current, DateOnly currentDate,
        string filerName)
    {
        Dictionary<string, Holding> prevByCusip = previous
            .GroupBy(h => h.Cusip)
            .ToDictionary(g => g.Key, g => AggregateHoldings(g.ToList()));

        Dictionary<string, Holding> currByCusip = current
            .GroupBy(h => h.Cusip)
            .ToDictionary(g => g.Key, g => AggregateHoldings(g.ToList()));

        var diff = new HoldingsDiff
        {
            FilerName = filerName,
            PreviousReportDate = previousDate,
            CurrentReportDate = currentDate
        };

        // Find new and changed positions
        foreach ((string cusip, Holding curr) in currByCusip)
        {
            if (prevByCusip.TryGetValue(cusip, out Holding? prev))
            {
                HoldingChange change = CreateChange(cusip, curr, prev);
                switch (change.ChangeType)
                {
                    case ChangeType.Increased:
                        diff.IncreasedPositions.Add(change);
                        break;
                    case ChangeType.Decreased:
                        diff.DecreasedPositions.Add(change);
                        break;
                    default:
                        diff.UnchangedPositions.Add(change);
                        break;
                }
            }
            else
            {
                diff.NewPositions.Add(new HoldingChange
                {
                    Cusip = cusip,
                    NameOfIssuer = curr.NameOfIssuer,
                    Ticker = curr.Ticker,
                    ChangeType = ChangeType.New,
                    PreviousShares = 0,
                    CurrentShares = curr.SharesOrPrincipalAmount,
                    PreviousValue = 0,
                    CurrentValue = curr.ReportedValue
                });
            }
        }

        // Find exited positions
        foreach ((string cusip, Holding prev) in prevByCusip)
        {
            if (!currByCusip.ContainsKey(cusip))
            {
                diff.ExitedPositions.Add(new HoldingChange
                {
                    Cusip = cusip,
                    NameOfIssuer = prev.NameOfIssuer,
                    Ticker = prev.Ticker,
                    ChangeType = ChangeType.Exited,
                    PreviousShares = prev.SharesOrPrincipalAmount,
                    CurrentShares = 0,
                    PreviousValue = prev.ReportedValue,
                    CurrentValue = 0
                });
            }
        }

        logger.LogInformation(
            "Diff complete: {New} new, {Inc} increased, {Dec} decreased, {Exit} exited, {Unch} unchanged",
            diff.NewPositions.Count, diff.IncreasedPositions.Count,
            diff.DecreasedPositions.Count, diff.ExitedPositions.Count,
            diff.UnchangedPositions.Count);

        return diff;
    }

    private static HoldingChange CreateChange(string cusip, Holding current, Holding previous)
    {
        long sharesDelta = current.SharesOrPrincipalAmount - previous.SharesOrPrincipalAmount;
        ChangeType changeType = sharesDelta switch
        {
            > 0 => ChangeType.Increased,
            < 0 => ChangeType.Decreased,
            _ => ChangeType.Unchanged
        };

        return new HoldingChange
        {
            Cusip = cusip,
            NameOfIssuer = current.NameOfIssuer,
            Ticker = current.Ticker,
            ChangeType = changeType,
            PreviousShares = previous.SharesOrPrincipalAmount,
            CurrentShares = current.SharesOrPrincipalAmount,
            PreviousValue = previous.ReportedValue,
            CurrentValue = current.ReportedValue
        };
    }

    /// <summary>
    /// Aggregate multiple holdings with the same CUSIP (e.g. different share classes).
    /// </summary>
    private static Holding AggregateHoldings(List<Holding> holdings)
    {
        if (holdings.Count == 1) return holdings[0];

        return new Holding
        {
            NameOfIssuer = holdings[0].NameOfIssuer,
            TitleOfClass = holdings[0].TitleOfClass,
            Cusip = holdings[0].Cusip,
            Ticker = holdings[0].Ticker,
            ReportedValue = holdings.Sum(h => h.ReportedValue),
            SharesOrPrincipalAmount = holdings.Sum(h => h.SharesOrPrincipalAmount),
            SharesOrPrincipalAmountType = holdings[0].SharesOrPrincipalAmountType
        };
    }
}
