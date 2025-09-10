using McpCodeEditor.Models.TypeScript;

namespace McpCodeEditor.Interfaces;

/// <summary>
/// Service for detecting expression boundaries in TypeScript/JavaScript code
/// Handles AST-based and pattern-based boundary detection for introduce variable operations
/// </summary>
public interface IExpressionBoundaryDetectionService
{
    /// <summary>
    /// Detect and adjust expression boundaries using multiple strategies
    /// Returns adjusted boundaries and the detected expression
    /// </summary>
    /// <param name="lineContent">The line of code containing the expression</param>
    /// <param name="startColumn">Starting column (1-based)</param>
    /// <param name="endColumn">Ending column (1-based)</param>
    /// <returns>Result containing success status, adjusted boundaries, and expression</returns>
    ExpressionBoundaryResult DetectExpressionBoundaries(string lineContent, int startColumn, int endColumn);
}
