namespace PlaywrightServerMcp.Models;

/// <summary>
/// Routing analysis
/// </summary>
public class RoutingAnalysis
{
    public bool RouterConfigured { get; set; }
    public bool PreloadingStrategy { get; set; }
    public bool GuardsConfigured { get; set; }
    public int RouteCount { get; set; }
}