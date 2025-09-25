using System.Text.RegularExpressions;

namespace McpCodeEditor.Services.Validation;

/// <summary>
/// Enhanced variable type inference helpers for complex extraction scenarios
/// </summary>
public static class VariableTypeInferenceHelper
{
    /// <summary>
    /// Enhanced variable type inference with better collection and method call detection
    /// </summary>
    public static List<(string Name, string Type)> InferVariableTypesEnhanced(string selectedCode)
    {
        var inferences = new List<(string Name, string Type)>();
        var processedVariables = new HashSet<string>();

        var lines = selectedCode.Split('\n');
        
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            
            // Enhanced Pattern 1: Variables used in foreach loops (better collection detection)
            var foreachMatch = Regex.Match(trimmed, @"foreach\s*\(\s*(\w+)\s+\w+\s+in\s+(\w+)\s*\)");
            if (foreachMatch.Success && !processedVariables.Contains(foreachMatch.Groups[2].Value))
            {
                var collectionName = foreachMatch.Groups[2].Value;
                var elementType = foreachMatch.Groups[1].Value;
                
                // Try to infer better collection type based on element type
                var collectionType = elementType switch
                {
                    "var" => "List<object>",
                    "string" => "List<string>",
                    "int" => "List<int>",
                    "Order" => "List<Order>",  // Custom types
                    _ => $"List<{elementType}>"
                };
                
                inferences.Add((collectionName, collectionType));
                processedVariables.Add(collectionName);
            }

            // Enhanced Pattern 2: Variables with arithmetic operations (better numeric detection)
            var arithmeticMatches = Regex.Matches(trimmed, @"(\w+)\s*([+\-*/]=|\+\+|--|[+\-*/])\s*(\w+|[\d.]+)");
            foreach (Match match in arithmeticMatches)
            {
                var varName = match.Groups[1].Value;
                if (!processedVariables.Contains(varName) && !IsKeyword(varName))
                {
                    // Better inference based on operation and naming
                    string inferredType;
                    if (varName.Contains("count", StringComparison.CurrentCultureIgnoreCase) || varName.Contains("total", StringComparison.CurrentCultureIgnoreCase) || 
                        varName.Contains("sum", StringComparison.CurrentCultureIgnoreCase) || varName.Contains("index", StringComparison.CurrentCultureIgnoreCase))
                    {
                        inferredType = "int";
                    }
                    else if (varName.Contains("average", StringComparison.CurrentCultureIgnoreCase) || varName.Contains("percent", StringComparison.CurrentCultureIgnoreCase) ||
                            varName.Contains("rate", StringComparison.CurrentCultureIgnoreCase) || varName.Contains("ratio", StringComparison.CurrentCultureIgnoreCase))
                    {
                        inferredType = "double";
                    }
                    else if (match.Groups[3].Value.Contains("."))
                    {
                        inferredType = "double";  // Decimal operations suggest double
                    }
                    else
                    {
                        inferredType = "int";
                    }
                    
                    inferences.Add((varName, inferredType));
                    processedVariables.Add(varName);
                }
            }

            // Enhanced Pattern 3: Variables used with property access (better object detection)
            var propertyMatches = Regex.Matches(trimmed, @"(\w+)\.(\w+)");
            foreach (Match match in propertyMatches)
            {
                var varName = match.Groups[1].Value;
                var propertyName = match.Groups[2].Value;
                
                if (!processedVariables.Contains(varName) && !IsKeyword(varName))
                {
                    // Try to infer better type based on property names
                    var inferredType = propertyName switch
                    {
                        "Status" or "Name" or "Title" => "Order",  // Common business object
                        "Amount" or "Price" or "Cost" => "Order",
                        "Count" or "Length" => "List<object>",
                        "Add" or "Remove" or "Contains" => "List<object>",
                        _ => "object"
                    };
                    
                    inferences.Add((varName, inferredType));
                    processedVariables.Add(varName);
                }
            }

            // Enhanced Pattern 4: Variable declarations with explicit types
            var declarationMatches = Regex.Matches(trimmed, @"\b(int|double|float|decimal|string|bool|var|List<\w+>)\s+(\w+)\s*=");
            foreach (Match match in declarationMatches)
            {
                var declaredType = match.Groups[1].Value;
                var varName = match.Groups[2].Value;
                
                if (!processedVariables.Contains(varName))
                {
                    // Use the declared type directly
                    inferences.Add((varName, declaredType == "var" ? "object" : declaredType));
                    processedVariables.Add(varName);
                }
            }

            // Enhanced Pattern 5: Method calls that suggest variable types
            var methodCallMatches = Regex.Matches(trimmed, @"(\w+)\s*=\s*\w+\.(\w+)\(");
            foreach (Match match in methodCallMatches)
            {
                var varName = match.Groups[1].Value;
                var methodName = match.Groups[2].Value;
                
                if (!processedVariables.Contains(varName) && !IsKeyword(varName))
                {
                    var inferredType = methodName switch
                    {
                        "Where" or "Select" or "OrderBy" => "IEnumerable<object>",
                        "FirstOrDefault" or "SingleOrDefault" => "object",
                        "Count" or "Sum" => "int",
                        "Average" => "double",
                        "ToString" => "string",
                        _ => "object"
                    };
                    
                    inferences.Add((varName, inferredType));
                    processedVariables.Add(varName);
                }
            }
        }

        return inferences;
    }

    /// <summary>
    /// Enhanced keyword detection with more C# keywords and common types
    /// </summary>
    private static bool IsKeyword(string word)
    {
        var keywords = new HashSet<string>
        {
            "var", "int", "string", "bool", "double", "float", "decimal", "object", "dynamic",
            "if", "else", "for", "foreach", "while", "do", "switch", "case", "default",
            "return", "break", "continue", "throw", "try", "catch", "finally",
            "class", "struct", "interface", "enum", "namespace", "using",
            "public", "private", "protected", "internal", "static", "void", "async", "await",
            "new", "this", "base", "null", "true", "false", "is", "as", "typeof", "sizeof",
            "checked", "unchecked", "lock", "fixed", "unsafe", "stackalloc", "nameof",
            "when", "where", "select", "from", "in", "on", "equals", "by", "ascending", "descending",
            "join", "let", "orderby", "group", "into", "ref", "out", "params", "readonly"
        };
        
        return keywords.Contains(word);
    }
}
