namespace Edgar.Core.Models;

public class TradeRecommendation
{
    public string Ticker { get; set; } = string.Empty;
    public string Cusip { get; set; } = string.Empty;
    public string NameOfIssuer { get; set; } = string.Empty;
    public TradeAction Action { get; set; }
    public decimal Quantity { get; set; }
    public string Reason { get; set; } = string.Empty;
    public decimal? EstimatedValue { get; set; }

    /// <summary>
    /// The weight of this position in the filer's portfolio (0-1).
    /// </summary>
    public double PortfolioWeight { get; set; }
}

public enum TradeAction
{
    Buy,
    Sell
}

public class TradeResult
{
    public string Ticker { get; set; } = string.Empty;
    public TradeAction Action { get; set; }
    public decimal RequestedQuantity { get; set; }
    public bool Success { get; set; }
    public string? OrderId { get; set; }
    public string? Error { get; set; }
}

public class SyncResult
{
    public string FilerName { get; set; } = string.Empty;
    public DateOnly PreviousReportDate { get; set; }
    public DateOnly CurrentReportDate { get; set; }
    public int TotalChangesDetected { get; set; }
    public int TradesGenerated { get; set; }
    public int TradesExecuted { get; set; }
    public int TradesFailed { get; set; }
    public List<TradeResult> Results { get; set; } = [];
    public decimal AccountValueUsed { get; set; }
    public string? Warning { get; set; }
}

public class MarketStatus
{
    public bool IsOpen { get; set; }
    public DateTime NextOpen { get; set; }
    public DateTime NextClose { get; set; }
}
