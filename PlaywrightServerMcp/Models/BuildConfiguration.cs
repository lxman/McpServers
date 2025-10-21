namespace PlaywrightServerMcp.Models;

/// <summary>
/// Build configuration details
/// </summary>
public class BuildConfiguration
{
    public string OutputPath { get; set; } = string.Empty;
    public bool Optimization { get; set; }
    public bool SourceMap { get; set; }
    public bool ExtractCss { get; set; }
    public bool NamedChunks { get; set; }
    public bool Aot { get; set; }
    public string BudgetType { get; set; } = string.Empty;
    public List<BudgetConfig> Budgets { get; set; } = [];
    public Dictionary<string, object> AdditionalOptions { get; set; } = new();
}