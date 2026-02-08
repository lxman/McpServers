namespace Edgar.Core.Models;

public class TrackedFiler
{
    public string Name { get; set; } = string.Empty;
    public string Cik { get; set; } = string.Empty;

    /// <summary>
    /// Returns the CIK zero-padded to 10 digits as required by SEC EDGAR.
    /// </summary>
    public string PaddedCik => Cik.PadLeft(10, '0');
}
