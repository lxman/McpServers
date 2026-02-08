namespace Edgar.Core.Models;

public class HoldingsDiff
{
    public string FilerName { get; set; } = string.Empty;
    public DateOnly PreviousReportDate { get; set; }
    public DateOnly CurrentReportDate { get; set; }
    public List<HoldingChange> NewPositions { get; set; } = [];
    public List<HoldingChange> IncreasedPositions { get; set; } = [];
    public List<HoldingChange> DecreasedPositions { get; set; } = [];
    public List<HoldingChange> ExitedPositions { get; set; } = [];
    public List<HoldingChange> UnchangedPositions { get; set; } = [];

    public int TotalChanges => NewPositions.Count + IncreasedPositions.Count +
                               DecreasedPositions.Count + ExitedPositions.Count;
}

public class HoldingChange
{
    public string Cusip { get; set; } = string.Empty;
    public string NameOfIssuer { get; set; } = string.Empty;
    public string? Ticker { get; set; }
    public ChangeType ChangeType { get; set; }
    public long PreviousShares { get; set; }
    public long CurrentShares { get; set; }
    public long SharesDelta => CurrentShares - PreviousShares;
    public decimal PreviousValue { get; set; }
    public decimal CurrentValue { get; set; }
    public decimal ValueDelta => CurrentValue - PreviousValue;
    public double PercentChange => PreviousShares > 0
        ? Math.Round((double)SharesDelta / PreviousShares * 100, 2)
        : CurrentShares > 0 ? 100.0 : 0.0;
}

public enum ChangeType
{
    New,
    Increased,
    Decreased,
    Exited,
    Unchanged
}
