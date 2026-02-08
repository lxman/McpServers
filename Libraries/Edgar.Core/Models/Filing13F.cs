namespace Edgar.Core.Models;

public class Filing13F
{
    public string FilerName { get; set; } = string.Empty;
    public string Cik { get; set; } = string.Empty;
    public string AccessionNumber { get; set; } = string.Empty;
    public DateOnly FilingDate { get; set; }
    public DateOnly ReportDate { get; set; }
    public string FormType { get; set; } = string.Empty;
    public string PrimaryDocument { get; set; } = string.Empty;

    /// <summary>
    /// Accession number formatted for URL paths (dashes removed).
    /// </summary>
    public string AccessionNumberForUrl => AccessionNumber.Replace("-", "");
}
