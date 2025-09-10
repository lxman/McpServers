namespace McpCodeEditor.Models.TypeScript;

/// <summary>
/// Options for TypeScript method extraction operations
/// </summary>
public class TypeScriptExtractionOptions
{
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public string NewMethodName { get; set; } = string.Empty;
    public string? ReturnType { get; set; }
    public string? AccessModifier { get; set; } = "private";
    public bool IsAsync { get; set; }
    public bool IsStatic { get; set; }
    public TypeScriptFunctionType FunctionType { get; set; } = TypeScriptFunctionType.Function;
    public bool ExportMethod { get; set; }
}

/// <summary>
/// Types of TypeScript function declarations
/// </summary>
public enum TypeScriptFunctionType
{
    Function,      // function methodName() {}
    ArrowFunction, // const methodName = () => {}
    Method,        // methodName() {} (within class)
    AsyncFunction, // async function methodName() {}
    AsyncArrowFunction // const methodName = async () => {}
}
