using McpCodeEditor.Interfaces;
using Microsoft.Extensions.Logging;

namespace McpCodeEditor.Services.CSharp;

/// <summary>
/// Service responsible for filtering and determining which variables should be passed as parameters
/// Distinguishes between class fields, static members, and local variables
/// SESSION 2 FIX: Created to properly filter parameters for method extraction
/// </summary>
public class ParameterFilteringService : IParameterFilteringService
{
    private readonly ILogger<ParameterFilteringService>? _logger;

    // Common class field patterns (this., base., private fields, etc.)
    private readonly HashSet<string> _commonFieldPrefixes = ["this.", "base.", "_"];

    // Common static types that should not be passed as parameters
    private readonly HashSet<string> _staticTypes =
    [
        "Console", "Math", "DateTime", "String", "Convert", "Environment",
        "File", "Directory", "Path", "Debug", "Trace", "Logger",
        "Encoding", "Regex", "Random", "Guid", "TimeSpan"
    ];

    public ParameterFilteringService(ILogger<ParameterFilteringService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Filters variables to determine which should be passed as parameters
    /// </summary>
    public List<VariableInfo> FilterParametersToPass(
        List<VariableInfo> externalVariables,
        string[] extractedLines,
        string[]? fullFileLines = null)
    {
        _logger?.LogDebug("Filtering {Count} external variables for parameter passing", externalVariables.Count);

        var parametersToPass = new List<VariableInfo>();

        foreach (var variable in externalVariables)
        {
            if (ShouldPassAsParameter(variable, extractedLines, fullFileLines))
            {
                parametersToPass.Add(variable);
                _logger?.LogDebug("Variable {Name} will be passed as parameter", variable.Name);
            }
            else
            {
                _logger?.LogDebug("Variable {Name} will NOT be passed as parameter (likely class field or static)", 
                    variable.Name);
            }
        }

        return parametersToPass;
    }

    /// <summary>
    /// Determines if a variable should be passed as a parameter
    /// </summary>
    public bool ShouldPassAsParameter(VariableInfo variable, string[] extractedLines, string[]? fullFileLines)
    {
        var varName = variable.Name;

        // Check 1: Skip static types and members
        if (IsStaticMember(varName))
        {
            _logger?.LogDebug("Variable {Name} is a static member - skipping", varName);
            return false;
        }

        // Check 2: Skip obvious field patterns
        if (IsObviousFieldPattern(varName, extractedLines))
        {
            _logger?.LogDebug("Variable {Name} matches field pattern - skipping", varName);
            return false;
        }

        // Check 3: If we have the full file, check if it's a class field
        if (fullFileLines != null && IsClassField(varName, fullFileLines))
        {
            _logger?.LogDebug("Variable {Name} is a class field - skipping", varName);
            return false;
        }

        // Check 4: Skip collection types that are likely class fields
        if (IsLikelyClassFieldCollection(varName, extractedLines))
        {
            _logger?.LogDebug("Variable {Name} is likely a class field collection - skipping", varName);
            return false;
        }

        // Default: Pass as parameter (likely a local variable)
        return true;
    }

    /// <summary>
    /// Checks if a variable is a static member or type
    /// </summary>
    private bool IsStaticMember(string varName)
    {
        // Check if it's a known static type
        if (_staticTypes.Contains(varName))
            return true;

        // Check for static member access pattern (Type.Member)
        if (varName.Contains('.'))
        {
            var typeName = varName.Split('.')[0];
            if (_staticTypes.Contains(typeName))
                return true;

            // Check if first letter is uppercase (likely a type)
            if (char.IsUpper(typeName[0]))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks for obvious field patterns in variable usage
    /// </summary>
    private static bool IsObviousFieldPattern(string varName, string[] extractedLines)
    {
        var extractedCode = string.Join(" ", extractedLines);

        // Check for this.varName or base.varName
        if (extractedCode.Contains($"this.{varName}") || extractedCode.Contains($"base.{varName}"))
            return true;

        // Check for underscore prefix (common field naming convention)
        if (varName.StartsWith("_"))
            return true;

        // Check for common field name patterns
        var fieldPatterns = new[] { "field", "member", "instance" };
        var lowerName = varName.ToLower();
        if (fieldPatterns.Any(pattern => lowerName.Contains(pattern)))
            return true;

        return false;
    }

    /// <summary>
    /// Checks if a variable is declared as a class field in the full file
    /// </summary>
    private bool IsClassField(string varName, string[] fullFileLines)
    {
        // Look for field declarations at class level
        // This is a simplified check - could be enhanced with proper syntax analysis
        
        var inClass = false;
        var braceDepth = 0;
        
        foreach (var line in fullFileLines)
        {
            var trimmedLine = line.Trim();
            
            // Track class entry
            if (trimmedLine.Contains("class ") && !trimmedLine.StartsWith("//"))
            {
                inClass = true;
                braceDepth = 0;
            }
            
            if (inClass)
            {
                // Count braces to track depth
                braceDepth += line.Count(c => c == '{');
                braceDepth -= line.Count(c => c == '}');
                
                // At class level (depth 1), look for field declarations
                if (braceDepth == 1 && !trimmedLine.StartsWith("//"))
                {
                    // Check for field declaration patterns
                    var fieldPatterns = new[]
                    {
                        $@"\b(private|public|protected|internal)\s+.*\s+{varName}\s*(=|;)",
                        $@"\b(readonly|static|const)\s+.*\s+{varName}\s*(=|;)",
                        $@"\b\w+\s+{varName}\s*(=|;)" // Simple field declaration
                    };
                    
                    foreach (var pattern in fieldPatterns)
                    {
                        if (System.Text.RegularExpressions.Regex.IsMatch(trimmedLine, pattern))
                        {
                            _logger?.LogDebug("Found field declaration for {Name}: {Line}", varName, trimmedLine);
                            return true;
                        }
                    }
                }
            }
        }
        
        return false;
    }

    /// <summary>
    /// Checks if a variable is likely a class field collection based on usage patterns
    /// </summary>
    private static bool IsLikelyClassFieldCollection(string varName, string[] extractedLines)
    {
        // Common collection field names
        var collectionNames = new[] { "list", "items", "collection", "data", "cache", "store", "repository" };
        var lowerName = varName.ToLower();
        
        // Check if it ends with 's' (plural) and is used in foreach
        if (varName.EndsWith("s") && varName.Length > 2)
        {
            var extractedCode = string.Join(" ", extractedLines);
            if (extractedCode.Contains($"foreach") && extractedCode.Contains($"in {varName}"))
            {
                // Likely a collection field if not declared in the extraction
                if (!extractedCode.Contains($"var {varName}") && 
                    !extractedCode.Contains($"List<") && 
                    !extractedCode.Contains($"new "))
                {
                    return true;
                }
            }
        }
        
        // Check for common collection field patterns
        return collectionNames.Any(pattern => lowerName.Contains(pattern));
    }

    /// <summary>
    /// Gets suggested parameter types based on usage
    /// </summary>
    public Dictionary<string, string> SuggestParameterTypes(
        List<VariableInfo> parameters,
        string[] extractedLines)
    {
        var suggestedTypes = new Dictionary<string, string>();
        
        foreach (var param in parameters)
        {
            var suggestedType = InferParameterType(param.Name, extractedLines);
            suggestedTypes[param.Name] = suggestedType;
            _logger?.LogDebug("Suggested type for parameter {Name}: {Type}", param.Name, suggestedType);
        }
        
        return suggestedTypes;
    }

    /// <summary>
    /// Infers the type of a parameter based on usage patterns
    /// </summary>
    private static string InferParameterType(string paramName, string[] extractedLines)
    {
        var extractedCode = string.Join(" ", extractedLines);
        
        // Check for explicit type usage patterns
        var typePatterns = new Dictionary<string, string>
        {
            [@$"{paramName}\.Count\b"] = "ICollection",
            [@$"{paramName}\.Length\b"] = "string",
            [@$"{paramName}\.Add\("] = "List<object>",
            [@$"{paramName}\.Contains\("] = "IEnumerable",
            [@$"foreach\s*\(.*\s+in\s+{paramName}"] = "IEnumerable",
            [@$"int\.TryParse\({paramName}"] = "string",
            [@$"{paramName}\s*[+\-*/]\s*\d"] = "int",
            [@$"{paramName}\.ToString\(\)"] = "object"
        };
        
        foreach ((var pattern, var type) in typePatterns)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(extractedCode, pattern))
            {
                return type;
            }
        }
        
        // Default based on naming conventions
        if (paramName.EndsWith("Count") || paramName.EndsWith("Index") || paramName.EndsWith("Id"))
            return "int";
        if (paramName.EndsWith("Name") || paramName.EndsWith("Text") || paramName.EndsWith("Message"))
            return "string";
        if (paramName.StartsWith("is") || paramName.StartsWith("has") || paramName.StartsWith("can"))
            return "bool";
        if (paramName.EndsWith("s") && paramName.Length > 2) // Plural - likely collection
            return "IEnumerable";
            
        // Default
        return "object";
    }
}
