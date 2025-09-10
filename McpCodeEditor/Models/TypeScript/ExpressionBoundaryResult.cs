namespace McpCodeEditor.Models.TypeScript;

/// <summary>
/// Result of expression boundary detection for TypeScript variable operations
/// Contains information about detected expression boundaries and detection method used
/// </summary>
public class ExpressionBoundaryResult
{
    public bool Success { get; set; }
    public string Expression { get; set; } = string.Empty;
    public int StartColumn { get; set; }  // 1-based
    public int EndColumn { get; set; }    // 1-based  
    public string DetectionMethod { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}
