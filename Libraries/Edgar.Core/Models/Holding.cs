namespace Edgar.Core.Models;

public class Holding
{
    public string NameOfIssuer { get; set; } = string.Empty;
    public string TitleOfClass { get; set; } = string.Empty;
    public string Cusip { get; set; } = string.Empty;
    /// <summary>
    /// Raw value from the 13F filing. Used only for ratio calculations, not absolute dollar amounts.
    /// </summary>
    public long ReportedValue { get; set; }
    public long SharesOrPrincipalAmount { get; set; }
    /// <summary>
    /// "SH" for shares, "PRN" for principal amount.
    /// </summary>
    public string SharesOrPrincipalAmountType { get; set; } = "SH";

    /// <summary>
    /// Ticker symbol resolved from CUSIP (populated after mapping).
    /// </summary>
    public string? Ticker { get; set; }
}
