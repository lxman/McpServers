namespace McpCodeEditor.Models.TypeScript;

/// <summary>
/// Types of syntax fixes that can be suggested
/// </summary>
public enum TypeScriptFixType
{
    AddMissingSemicolon,
    AddMissingParentheses,
    FixDeclarationType,
    FixScopePosition,
    AddMissingImport,
    FixAccessModifier,
    AddTypeAnnotation,
    RemoveUnusedCode,
    FixIndentation,
    Other
}
