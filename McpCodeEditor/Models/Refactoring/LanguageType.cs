namespace McpCodeEditor.Models.Refactoring;

/// <summary>
/// Enumeration of supported programming languages for refactoring operations
/// </summary>
public enum LanguageType
{
    /// <summary>
    /// C# programming language (.cs files)
    /// </summary>
    CSharp,
    
    /// <summary>
    /// TypeScript programming language (.ts, .tsx files)
    /// </summary>
    TypeScript,
    
    /// <summary>
    /// JavaScript programming language (.js, .jsx files)
    /// </summary>
    JavaScript,
    
    /// <summary>
    /// Unknown or unsupported language
    /// </summary>
    Unknown
}
