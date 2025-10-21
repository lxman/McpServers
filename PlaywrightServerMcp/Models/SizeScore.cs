namespace PlaywrightServerMcp.Models;

/// <summary>
/// Bundle size scoring
/// </summary>
public class SizeScore
{
    public int Overall { get; set; } // 0-100
    public int InitialLoad { get; set; }
    public int LazyLoading { get; set; }
    public int VendorOptimization { get; set; }
    public string Grade { get; set; } = string.Empty; // A, B, C, D, F
    public List<string> ScoreFactors { get; set; } = [];
}