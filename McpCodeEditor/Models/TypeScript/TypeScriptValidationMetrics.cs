namespace McpCodeEditor.Models.TypeScript;

/// <summary>
/// Performance metrics for validation operations
/// </summary>
public class TypeScriptValidationMetrics
{
    /// <summary>
    /// Time taken for validation in milliseconds
    /// </summary>
    public long ValidationTimeMs { get; set; }

    /// <summary>
    /// Number of syntax patterns checked
    /// </summary>
    public int PatternsChecked { get; set; }

    /// <summary>
    /// Number of validation rules applied
    /// </summary>
    public int RulesApplied { get; set; }

    /// <summary>
    /// Whether TypeScript compiler was used
    /// </summary>
    public bool UsedTypeScriptCompiler { get; set; }
}
