using McpCodeEditor.Interfaces;
using McpCodeEditor.Models;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using McpCodeEditor.Models.Refactoring;
using McpCodeEditor.Models.TypeScript;

namespace McpCodeEditor.Services.Refactoring.TypeScript;

/// <summary>
/// Service for inlining TypeScript functions by replacing call sites with function body
/// Handles all TypeScript function types: function declarations, arrow functions, async functions, methods
/// </summary>
public class TypeScriptMethodInliner(
    ILogger<TypeScriptMethodInliner> logger,
    IPathValidationService pathValidationService)
    : ITypeScriptMethodInliner
{
    /// <summary>
    /// Inline a TypeScript function by replacing all call sites with function body
    /// </summary>
    public async Task<RefactoringResult> InlineFunctionAsync(
        string filePath,
        string functionName,
        string inlineScope = "file",
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Starting TypeScript function inlining: {FunctionName} in {FilePath}, scope: {Scope}",
                functionName, filePath, inlineScope);

            // Validate file path
            string resolvedPath;
            try
            {
                resolvedPath = pathValidationService.ValidateAndResolvePath(filePath);
            }
            catch (Exception ex)
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }

            // Validate TypeScript file
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            if (!IsTypeScriptFile(extension))
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"File is not a TypeScript/JavaScript file: {extension}"
                };
            }

            // Read file content
            var lines = await File.ReadAllLinesAsync(resolvedPath, cancellationToken);

            // Find the function to inline
            var functionInfo = FindTypeScriptFunction(lines, functionName);
            if (functionInfo == null)
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"Function '{functionName}' not found in file"
                };
            }

            // Validate function can be inlined
            var validationResult = ValidateFunctionForInlining(functionInfo, lines);
            if (!validationResult.CanInline)
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = validationResult.ErrorMessage ?? "Function cannot be inlined"
                };
            }

            // Extract function body
            var functionBody = ExtractFunctionBody(lines, functionInfo);
            if (string.IsNullOrWhiteSpace(functionBody))
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = "Function body is empty or could not be extracted"
                };
            }

            // Find all function calls
            var functionCalls = FindFunctionCalls(lines, functionName, functionInfo);
            if (functionCalls.Count == 0)
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"No calls to function '{functionName}' found"
                };
            }

            // Create modified content
            var modifiedLines = new List<string>(lines);

            // Replace function calls with inlined body (process in reverse order to maintain line numbers)
            foreach (var call in functionCalls.OrderByDescending(c => c.LineNumber))
            {
                var inlinedCode = CreateInlinedCode(functionBody, call, functionInfo);
                var originalLine = modifiedLines[call.LineNumber - 1];
                var modifiedLine = ReplaceFunctionCall(originalLine, call, inlinedCode);
                
                modifiedLines[call.LineNumber - 1] = modifiedLine;
            }

            // Remove function definition
            for (var i = functionInfo.EndLine - 1; i >= functionInfo.StartLine - 1; i--)
            {
                modifiedLines.RemoveAt(i);
            }

            // Create FileChange for change tracking
            var changes = new List<FileChange>
            {
                new FileChange
                {
                    FilePath = filePath,
                    OriginalContent = string.Join(Environment.NewLine, lines),
                    ModifiedContent = string.Join(Environment.NewLine, modifiedLines),
                    ChangeType = "InlineTypeScriptFunction"
                }
            };

            if (previewOnly)
            {
                return new RefactoringResult
                {
                    Success = true,
                    Message = $"Preview: Would inline TypeScript function '{functionName}' at {functionCalls.Count} call sites",
                    Changes = changes
                };
            }

            // Write modified content
            await File.WriteAllLinesAsync(resolvedPath, modifiedLines, cancellationToken);

            return new RefactoringResult
            {
                Success = true,
                Message = $"Successfully inlined TypeScript function '{functionName}' at {functionCalls.Count} call sites",
                FilesAffected = 1,
                Changes = changes
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TypeScript function inlining failed for {FunctionName} in {FilePath}", functionName, filePath);
            return new RefactoringResult
            {
                Success = false,
                Error = $"TypeScript function inlining failed: {ex.Message}"
            };
        }
    }

    #region Private Helper Methods

    private static bool IsTypeScriptFile(string extension)
    {
        return extension switch
        {
            ".ts" or ".tsx" or ".js" or ".jsx" => true,
            _ => false
        };
    }

    private static TypeScriptFunctionInfo? FindTypeScriptFunction(string[] lines, string functionName)
    {
        // Pattern for different function types
        var patterns = new[]
        {
            $@"^\s*function\s+{Regex.Escape(functionName)}\s*\(", // function declaration
            $@"^\s*const\s+{Regex.Escape(functionName)}\s*=\s*\(", // arrow function const
            $@"^\s*let\s+{Regex.Escape(functionName)}\s*=\s*\(", // arrow function let
            $@"^\s*var\s+{Regex.Escape(functionName)}\s*=\s*\(", // arrow function var
            $@"^\s*{Regex.Escape(functionName)}\s*:\s*\(", // object method
            $@"^\s*{Regex.Escape(functionName)}\s*\(" // method shorthand
        };

        for (var i = 0; i < lines.Length; i++)
        {
            foreach (var pattern in patterns)
            {
                if (Regex.IsMatch(lines[i], pattern))
                {
                    var functionInfo = new TypeScriptFunctionInfo
                    {
                        Name = functionName,
                        StartLine = i + 1,
                        FunctionType = DetermineFunctionType(lines[i])
                    };

                    // Find end of function
                    functionInfo.EndLine = FindFunctionEnd(lines, i, functionInfo.FunctionType);
                    
                    // Extract parameters
                    functionInfo.Parameters = ExtractParameters(lines[i]);

                    return functionInfo;
                }
            }
        }

        return null;
    }

    private static TypeScriptFunctionType DetermineFunctionType(string line)
    {
        if (line.Contains("async"))
        {
            return line.Contains("=>") ? TypeScriptFunctionType.AsyncArrowFunction : TypeScriptFunctionType.AsyncFunction;
        }
        
        if (line.Contains("=>"))
            return TypeScriptFunctionType.ArrowFunction;
        
        if (line.TrimStart().StartsWith("function"))
            return TypeScriptFunctionType.Function;

        return TypeScriptFunctionType.Method;
    }

    private static int FindFunctionEnd(string[] lines, int startLine, TypeScriptFunctionType functionType)
    {
        // For arrow functions with single expression
        if (functionType is TypeScriptFunctionType.ArrowFunction or TypeScriptFunctionType.AsyncArrowFunction)
        {
            if (lines[startLine].Contains("=>") && !lines[startLine].Contains("{"))
            {
                // Single line arrow function
                return startLine + 1;
            }
        }

        // Find matching closing brace
        var braceCount = 0;
        var foundOpenBrace = false;

        for (var i = startLine; i < lines.Length; i++)
        {
            foreach (var c in lines[i])
            {
                if (c == '{')
                {
                    braceCount++;
                    foundOpenBrace = true;
                }
                else if (c == '}')
                {
                    braceCount--;
                    if (foundOpenBrace && braceCount == 0)
                    {
                        return i + 1;
                    }
                }
            }
        }

        return startLine + 1; // Fallback
    }

    private static List<string> ExtractParameters(string functionLine)
    {
        var parameters = new List<string>();
        
        var match = Regex.Match(functionLine, @"\(([^)]*)\)");
        if (match.Success)
        {
            var paramStr = match.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(paramStr))
            {
                parameters.AddRange(paramStr.Split(',').Select(p => p.Trim().Split(':')[0].Trim()));
            }
        }

        return parameters;
    }

    private static FunctionInlineValidation ValidateFunctionForInlining(TypeScriptFunctionInfo functionInfo, string[] lines)
    {
        var validation = new FunctionInlineValidation { CanInline = true };

        // Check for recursive calls
        for (var i = functionInfo.StartLine - 1; i < functionInfo.EndLine; i++)
        {
            if (lines[i].Contains(functionInfo.Name + "("))
            {
                validation.CanInline = false;
                validation.ErrorMessage = "Cannot inline recursive function";
                return validation;
            }
        }

        // Check for complex control flow
        var complexPatterns = new[] { "return", "throw", "yield", "await" };
        var complexityCount = 0;
        
        for (var i = functionInfo.StartLine - 1; i < functionInfo.EndLine; i++)
        {
            foreach (var pattern in complexPatterns)
            {
                if (lines[i].Contains(pattern))
                    complexityCount++;
            }
        }

        if (complexityCount > 3)
        {
            validation.CanInline = false;
            validation.ErrorMessage = "Function is too complex for safe inlining";
        }

        return validation;
    }

    private static string ExtractFunctionBody(string[] lines, TypeScriptFunctionInfo functionInfo)
    {
        var bodyLines = new List<string>();

        // Handle different function types
        if (functionInfo.FunctionType is TypeScriptFunctionType.ArrowFunction or TypeScriptFunctionType.AsyncArrowFunction)
        {
            var firstLine = lines[functionInfo.StartLine - 1];
            if (firstLine.Contains("=>") && !firstLine.Contains("{"))
            {
                // Single expression arrow function
                var arrowIndex = firstLine.IndexOf("=>", StringComparison.Ordinal);
                return firstLine[(arrowIndex + 2)..].Trim().TrimEnd(';');
            }
        }

        // Extract body between braces
        var inBody = false;
        for (var i = functionInfo.StartLine - 1; i < functionInfo.EndLine; i++)
        {
            var line = lines[i];
            
            if (!inBody && line.Contains("{"))
            {
                inBody = true;
                var braceIndex = line.IndexOf("{", StringComparison.Ordinal);
                var afterBrace = line[(braceIndex + 1)..].Trim();
                if (!string.IsNullOrEmpty(afterBrace))
                    bodyLines.Add(afterBrace);
                continue;
            }

            if (inBody)
            {
                if (line.Contains("}"))
                {
                    var braceIndex = line.IndexOf("}", StringComparison.Ordinal);
                    var beforeBrace = line[..braceIndex].Trim();
                    if (!string.IsNullOrEmpty(beforeBrace))
                        bodyLines.Add(beforeBrace);
                    break;
                }
                else
                {
                    bodyLines.Add(line);
                }
            }
        }

        return string.Join(Environment.NewLine, bodyLines).Trim();
    }

    private static List<TypeScriptFunctionCall> FindFunctionCalls(string[] lines, string functionName, TypeScriptFunctionInfo functionInfo)
    {
        var calls = new List<TypeScriptFunctionCall>();
        var pattern = $@"\b{Regex.Escape(functionName)}\s*\(([^)]*)\)";

        for (var i = 0; i < lines.Length; i++)
        {
            // Skip the function definition itself
            if (i >= functionInfo.StartLine - 1 && i < functionInfo.EndLine)
                continue;

            var matches = Regex.Matches(lines[i], pattern);
            foreach (Match match in matches)
            {
                calls.Add(new TypeScriptFunctionCall
                {
                    LineNumber = i + 1,
                    Column = match.Index + 1,
                    CallText = match.Value,
                    Arguments = match.Groups[1].Value.Split(',').Select(arg => arg.Trim()).ToList()
                });
            }
        }

        return calls;
    }

    private static string CreateInlinedCode(string functionBody, TypeScriptFunctionCall call, TypeScriptFunctionInfo functionInfo)
    {
        var inlinedCode = functionBody;

        // Replace parameters with arguments
        for (var i = 0; i < Math.Min(functionInfo.Parameters.Count, call.Arguments.Count); i++)
        {
            var parameter = functionInfo.Parameters[i];
            var argument = call.Arguments[i];
            
            // Replace parameter with argument in the body
            inlinedCode = Regex.Replace(inlinedCode, $@"\b{Regex.Escape(parameter)}\b", argument);
        }

        // Handle return statements
        inlinedCode = Regex.Replace(inlinedCode, @"\breturn\s+", "");

        return inlinedCode.Trim();
    }

    private static string ReplaceFunctionCall(string line, TypeScriptFunctionCall call, string inlinedCode)
    {
        return line[..(call.Column - 1)] + 
               "(" + inlinedCode + ")" + 
               line[(call.Column - 1 + call.CallText.Length)..];
    }

    #endregion
}

// Supporting interfaces and models
public interface ITypeScriptMethodInliner
{
    Task<RefactoringResult> InlineFunctionAsync(
        string filePath,
        string functionName,
        string inlineScope = "file",
        bool previewOnly = false,
        CancellationToken cancellationToken = default);
}

public class TypeScriptFunctionInfo
{
    public string Name { get; set; } = string.Empty;
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public TypeScriptFunctionType FunctionType { get; set; }
    public List<string> Parameters { get; set; } = [];
}

public class TypeScriptFunctionCall
{
    public int LineNumber { get; set; }
    public int Column { get; set; }
    public string CallText { get; set; } = string.Empty;
    public List<string> Arguments { get; set; } = [];
}

public class FunctionInlineValidation
{
    public bool CanInline { get; set; }
    public string? ErrorMessage { get; set; }
}
