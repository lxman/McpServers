using McpCodeEditor.Services.Refactoring.TypeScript;

namespace McpCodeEditor.Models.TypeScript;

/// <summary>
/// Represents a TypeScript variable declaration with location and scope information
/// Contains details about where a variable is declared and its declaration type
/// </summary>
public class TypeScriptVariableDeclaration
{
    public int LineNumber { get; set; }
    public int Column { get; set; }
    public string DeclarationType { get; set; } = string.Empty; // var, let, const
    public TypeScriptScopeType ScopeType { get; set; }
}
