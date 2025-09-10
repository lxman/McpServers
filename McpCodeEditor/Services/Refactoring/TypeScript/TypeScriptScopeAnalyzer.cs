using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace McpCodeEditor.Services.Refactoring.TypeScript;

/// <summary>
/// Analyzes TypeScript file scope context to determine appropriate variable declaration strategies
/// Handles class scope, method scope, constructor scope, and module scope analysis
/// </summary>
public class TypeScriptScopeAnalyzer(ILogger<TypeScriptScopeAnalyzer> logger)
{
    /// <summary>
    /// Analyze the scope context at the given line position
    /// </summary>
    public TypeScriptScopeAnalysisResult AnalyzeScope(string[] lines, int targetLine)
    {
        try
        {
            logger.LogDebug("Analyzing TypeScript scope at line {Line}", targetLine);

            var result = new TypeScriptScopeAnalysisResult
            {
                TargetLine = targetLine,
                Success = true
            };

            // Convert to 0-based indexing for array access
            int lineIndex = targetLine - 1;
            
            // Validate line index
            if (lineIndex < 0 || lineIndex >= lines.Length)
            {
                return new TypeScriptScopeAnalysisResult
                {
                    Success = false,
                    ErrorMessage = $"Line {targetLine} is out of range"
                };
            }

            // Find the scope hierarchy from the target line upward
            List<TypeScriptScopeInfo> scopeHierarchy = AnalyzeScopeHierarchy(lines, lineIndex);
            result.ScopeHierarchy = scopeHierarchy;

            // Determine the primary scope type
            result.PrimaryScopeType = DeterminePrimaryScopeType(scopeHierarchy);

            // Analyze class context if we're in a class
            if (scopeHierarchy.Any(s => s.ScopeType == TypeScriptScopeType.Class))
            {
                result.ClassContext = AnalyzeClassContext(lines, lineIndex, scopeHierarchy);
            }

            // Determine the appropriate variable placement strategy
            result.VariablePlacementStrategy = DetermineVariablePlacementStrategy(result);

            logger.LogDebug("Scope analysis completed: {ScopeType}, Strategy: {Strategy}", 
                result.PrimaryScopeType, result.VariablePlacementStrategy.DeclarationType);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Scope analysis failed for line {Line}", targetLine);
            return new TypeScriptScopeAnalysisResult
            {
                Success = false,
                ErrorMessage = $"Scope analysis failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Analyze the scope hierarchy from the target line upward
    /// </summary>
    private static List<TypeScriptScopeInfo> AnalyzeScopeHierarchy(string[] lines, int startLineIndex)
    {
        var hierarchy = new List<TypeScriptScopeInfo>();
        var braceStack = new Stack<TypeScriptScopeInfo>();

        // Scan from the beginning of the file to the target line to build accurate scope context
        for (var i = 0; i <= startLineIndex; i++)
        {
            string line = lines[i].Trim();
            
            // Skip empty lines and comments
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//") || line.StartsWith("/*"))
                continue;

            // Detect scope-opening patterns
            TypeScriptScopeInfo? scopeInfo = DetectScopeOpening(line, i + 1);
            if (scopeInfo != null)
            {
                braceStack.Push(scopeInfo);
                hierarchy.Add(scopeInfo);
            }

            // Handle closing braces
            if (line.Contains("}"))
            {
                int braceCount = line.Count(c => c == '}');
                for (var j = 0; j < braceCount && braceStack.Count > 0; j++)
                {
                    TypeScriptScopeInfo closedScope = braceStack.Pop();
                    closedScope.EndLine = i + 1;
                }
            }
        }

        // Return only the scopes that are still active at the target line
        return hierarchy.Where(s => s.StartLine <= startLineIndex + 1 && 
                                  (s.EndLine == 0 || s.EndLine > startLineIndex + 1)).ToList();
    }

    /// <summary>
    /// Detect if a line opens a new scope
    /// </summary>
    private static TypeScriptScopeInfo? DetectScopeOpening(string line, int lineNumber)
    {
        // Class detection
        if (Regex.IsMatch(line, @"\b(export\s+)?(abstract\s+)?class\s+\w+"))
        {
            Match match = Regex.Match(line, @"\b(export\s+)?(abstract\s+)?class\s+(\w+)");
            return new TypeScriptScopeInfo
            {
                ScopeType = TypeScriptScopeType.Class,
                StartLine = lineNumber,
                Name = match.Success ? match.Groups[3].Value : "UnknownClass",
                IsExported = line.Contains("export")
            };
        }

        // Interface detection
        if (Regex.IsMatch(line, @"\b(export\s+)?interface\s+\w+"))
        {
            Match match = Regex.Match(line, @"\b(export\s+)?interface\s+(\w+)");
            return new TypeScriptScopeInfo
            {
                ScopeType = TypeScriptScopeType.Interface,
                StartLine = lineNumber,
                Name = match.Success ? match.Groups[2].Value : "UnknownInterface",
                IsExported = line.Contains("export")
            };
        }

        // Method detection (including constructors, getters, setters)
        if (Regex.IsMatch(line, @"\b(constructor|get|set|\w+)\s*\([^)]*\)\s*(\:\s*\w+)?\s*\{"))
        {
            Match match = Regex.Match(line, @"\b(constructor|get|set|(\w+))\s*\([^)]*\)");
            string methodName = match.Groups[1].Value == "constructor" ? "constructor" : 
                               match.Groups[1].Value is "get" or "set" ? 
                               match.Groups[1].Value : match.Groups[2].Value;
            
            return new TypeScriptScopeInfo
            {
                ScopeType = methodName == "constructor" ? TypeScriptScopeType.Constructor : TypeScriptScopeType.Method,
                StartLine = lineNumber,
                Name = methodName,
                AccessModifier = ExtractAccessModifier(line)
            };
        }

        // Function detection
        if (Regex.IsMatch(line, @"\b(export\s+)?(async\s+)?function\s+\w+"))
        {
            Match match = Regex.Match(line, @"\b(export\s+)?(async\s+)?function\s+(\w+)");
            return new TypeScriptScopeInfo
            {
                ScopeType = TypeScriptScopeType.Function,
                StartLine = lineNumber,
                Name = match.Success ? match.Groups[3].Value : "UnknownFunction",
                IsExported = line.Contains("export"),
                IsAsync = line.Contains("async")
            };
        }

        // Arrow function detection
        if (Regex.IsMatch(line, @"=\s*(\([^)]*\)\s*=>\s*\{|\w+\s*=>\s*\{)"))
        {
            return new TypeScriptScopeInfo
            {
                ScopeType = TypeScriptScopeType.ArrowFunction,
                StartLine = lineNumber,
                Name = "ArrowFunction"
            };
        }

        // Generic block detection (if, for, while, etc.)
        if (Regex.IsMatch(line, @"\b(if|for|while|switch|try|catch|finally)\b.*\{"))
        {
            Match match = Regex.Match(line, @"\b(if|for|while|switch|try|catch|finally)\b");
            return new TypeScriptScopeInfo
            {
                ScopeType = TypeScriptScopeType.Block,
                StartLine = lineNumber,
                Name = match.Groups[1].Value
            };
        }

        return null;
    }

    /// <summary>
    /// Extract access modifier from a line
    /// </summary>
    private static string ExtractAccessModifier(string line)
    {
        if (line.Contains("private")) return "private";
        if (line.Contains("protected")) return "protected";
        if (line.Contains("public")) return "public";
        return "public"; // Default in TypeScript
    }

    /// <summary>
    /// Determine the primary scope type from the hierarchy
    /// </summary>
    private static TypeScriptScopeType DeterminePrimaryScopeType(List<TypeScriptScopeInfo> hierarchy)
    {
        if (hierarchy.Count == 0) return TypeScriptScopeType.Module;

        // Return the most specific (deepest) scope
        TypeScriptScopeInfo lastScope = hierarchy.Last();
        return lastScope.ScopeType;
    }

    /// <summary>
    /// Analyze class-specific context
    /// </summary>
    private static TypeScriptClassContext? AnalyzeClassContext(string[] lines, int lineIndex, List<TypeScriptScopeInfo> hierarchy)
    {
        TypeScriptScopeInfo? classScope = hierarchy.FirstOrDefault(s => s.ScopeType == TypeScriptScopeType.Class);
        if (classScope == null) return null;

        return new TypeScriptClassContext
        {
            ClassName = classScope.Name,
            IsExported = classScope.IsExported,
            IsInMethod = hierarchy.Any(s => s.ScopeType == TypeScriptScopeType.Method),
            IsInConstructor = hierarchy.Any(s => s.ScopeType == TypeScriptScopeType.Constructor),
            CurrentAccessLevel = DetermineCurrentAccessLevel(lines, lineIndex, hierarchy)
        };
    }

    /// <summary>
    /// Determine the current access level context within a class
    /// </summary>
    private static string DetermineCurrentAccessLevel(string[] lines, int lineIndex, List<TypeScriptScopeInfo> hierarchy)
    {
        // If we're in a method, use the method's access modifier
        TypeScriptScopeInfo? methodScope = hierarchy.LastOrDefault(s => s.ScopeType is TypeScriptScopeType.Method or TypeScriptScopeType.Constructor);
        if (methodScope != null)
        {
            return methodScope.AccessModifier ?? "public";
        }

        return "private"; // Default for class members
    }

    /// <summary>
    /// Determine the appropriate variable placement strategy based on scope analysis
    /// </summary>
    private static TypeScriptVariablePlacementStrategy DetermineVariablePlacementStrategy(TypeScriptScopeAnalysisResult scopeResult)
    {
        switch (scopeResult.PrimaryScopeType)
        {
            case TypeScriptScopeType.Class:
                // Direct class scope - variable should be a class member
                return new TypeScriptVariablePlacementStrategy
                {
                    DeclarationType = "private readonly",
                    PlacementLocation = VariablePlacementLocation.ClassMember,
                    RequiresThisPrefix = false,
                    SuggestedAccessModifier = "private"
                };

            case TypeScriptScopeType.Constructor:
                // Constructor scope - can use const/let locally or assign to this.property
                if (scopeResult.ClassContext != null)
                {
                    return new TypeScriptVariablePlacementStrategy
                    {
                        DeclarationType = "const",
                        PlacementLocation = VariablePlacementLocation.MethodLocal,
                        RequiresThisPrefix = false,
                        AlternativeStrategy = new TypeScriptVariablePlacementStrategy
                        {
                            DeclarationType = "private readonly",
                            PlacementLocation = VariablePlacementLocation.ClassMember,
                            RequiresThisPrefix = true,
                            SuggestedAccessModifier = "private"
                        }
                    };
                }
                goto case TypeScriptScopeType.Method;

            case TypeScriptScopeType.Method:
                // Method scope - use const/let for local variables
                return new TypeScriptVariablePlacementStrategy
                {
                    DeclarationType = "const",
                    PlacementLocation = VariablePlacementLocation.MethodLocal,
                    RequiresThisPrefix = false
                };

            case TypeScriptScopeType.Function:
            case TypeScriptScopeType.ArrowFunction:
                // Function scope - use const/let
                return new TypeScriptVariablePlacementStrategy
                {
                    DeclarationType = "const",
                    PlacementLocation = VariablePlacementLocation.FunctionLocal,
                    RequiresThisPrefix = false
                };

            case TypeScriptScopeType.Block:
                // Block scope - use const/let
                return new TypeScriptVariablePlacementStrategy
                {
                    DeclarationType = "const",
                    PlacementLocation = VariablePlacementLocation.BlockLocal,
                    RequiresThisPrefix = false
                };

            case TypeScriptScopeType.Module:
            default:
                // Module scope - use const/let
                return new TypeScriptVariablePlacementStrategy
                {
                    DeclarationType = "const",
                    PlacementLocation = VariablePlacementLocation.ModuleLevel,
                    RequiresThisPrefix = false
                };
        }
    }
}

/// <summary>
/// Result of TypeScript scope analysis
/// </summary>
public class TypeScriptScopeAnalysisResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int TargetLine { get; set; }
    public TypeScriptScopeType PrimaryScopeType { get; set; }
    public List<TypeScriptScopeInfo> ScopeHierarchy { get; set; } = [];
    public TypeScriptClassContext? ClassContext { get; set; }
    public TypeScriptVariablePlacementStrategy VariablePlacementStrategy { get; set; } = new();
}

/// <summary>
/// Information about a TypeScript scope
/// </summary>
public class TypeScriptScopeInfo
{
    public TypeScriptScopeType ScopeType { get; set; }
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? AccessModifier { get; set; }
    public bool IsExported { get; set; }
    public bool IsAsync { get; set; }
}

/// <summary>
/// Class-specific context information
/// </summary>
public class TypeScriptClassContext
{
    public string ClassName { get; set; } = string.Empty;
    public bool IsExported { get; set; }
    public bool IsInMethod { get; set; }
    public bool IsInConstructor { get; set; }
    public string CurrentAccessLevel { get; set; } = "private";
}

/// <summary>
/// Strategy for placing variables in TypeScript code
/// </summary>
public class TypeScriptVariablePlacementStrategy
{
    public string DeclarationType { get; set; } = "const";
    public VariablePlacementLocation PlacementLocation { get; set; }
    public bool RequiresThisPrefix { get; set; }
    public string? SuggestedAccessModifier { get; set; }
    public TypeScriptVariablePlacementStrategy? AlternativeStrategy { get; set; }
}

/// <summary>
/// Enhanced TypeScript scope types
/// </summary>
public enum TypeScriptScopeType
{
    Module,
    Class,
    Interface,
    Method,
    Constructor,
    Function,
    ArrowFunction,
    Block
}

/// <summary>
/// Variable placement location options
/// </summary>
public enum VariablePlacementLocation
{
    ModuleLevel,
    ClassMember,
    MethodLocal,
    FunctionLocal,
    BlockLocal
}
