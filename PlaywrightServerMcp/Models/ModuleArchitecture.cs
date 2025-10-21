namespace PlaywrightServerMcp.Models;

/// <summary>
/// Module architecture analysis
/// </summary>
public class ModuleArchitecture
{
    public bool UsesStandaloneComponents { get; set; }
    public bool UsesNgModules { get; set; }
    public bool MixedArchitecture { get; set; }
    public LazyLoadingAnalysis LazyLoading { get; set; } = new();
    public RoutingAnalysis Routing { get; set; } = new();
}