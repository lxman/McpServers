namespace PlaywrightServerMcp.Models;

/// <summary>
/// Compatibility matrix for Angular ecosystem
/// </summary>
public class CompatibilityMatrix
{
    public bool NodeCompatible { get; set; }
    public bool TypeScriptCompatible { get; set; }
    public bool RxJsCompatible { get; set; }
    public List<string> IncompatiblePackages { get; set; } = [];
}