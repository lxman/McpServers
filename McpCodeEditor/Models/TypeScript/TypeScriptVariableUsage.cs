namespace McpCodeEditor.Models.TypeScript;

/// <summary>
/// Represents a TypeScript variable usage with location and usage type information
/// Contains details about where and how a variable is used in the code
/// </summary>
public class TypeScriptVariableUsage
{
    public int LineNumber { get; set; }
    public int Column { get; set; }
    public TypeScriptVariableUsageType UsageType { get; set; }
}

/// <summary>
/// Enumeration of TypeScript variable usage types
/// Defines how a variable is being used at a specific location
/// </summary>
public enum TypeScriptVariableUsageType
{
    Read,
    Assignment,
    Modification
}
