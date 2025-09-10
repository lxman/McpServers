namespace McpCodeEditor.Models.TypeScript;

/// <summary>
/// Result of TypeScript type inference analysis
/// Phase A2-T2: Model for TypeScriptAstAnalysisService
/// </summary>
public class TypeScriptTypeInference
{
    /// <summary>
    /// Whether type inference was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Inferred TypeScript type (e.g., "string", "number", "boolean", "MyInterface")
    /// </summary>
    public string InferredType { get; set; } = string.Empty;

    /// <summary>
    /// Confidence level of the type inference (0.0 to 1.0)
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Whether the type is nullable
    /// </summary>
    public bool IsNullable { get; set; }

    /// <summary>
    /// Whether the type is a union type
    /// </summary>
    public bool IsUnionType { get; set; }

    /// <summary>
    /// Union type members if IsUnionType is true
    /// </summary>
    public List<string> UnionTypes { get; set; } = [];

    /// <summary>
    /// Error message if inference failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Method used for type inference (AST, Pattern, Heuristic)
    /// </summary>
    public string InferenceMethod { get; set; } = string.Empty;
}
