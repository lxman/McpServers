using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using McpCodeEditor.Interfaces;
using Esprima;
using Esprima.Ast;
using McpCodeEditor.Models.TypeScript;

namespace McpCodeEditor.Services.TypeScript;

/// <summary>
/// Service for detecting expression boundaries in TypeScript/JavaScript code
/// Extracted from TypeScriptVariableOperations for better separation of concerns
/// Enhanced with Esprima AST parsing for reliable boundary detection
/// </summary>
public class ExpressionBoundaryDetectionService : IExpressionBoundaryDetectionService
{
    private readonly ILogger<ExpressionBoundaryDetectionService> _logger;

    public ExpressionBoundaryDetectionService(ILogger<ExpressionBoundaryDetectionService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// REF-001 FIXED: Automatically detects and adjusts expression boundaries using AST parsing
    /// Enhanced with Esprima for reliable TypeScript/JavaScript parsing and proper boundary expansion
    /// </summary>
    public ExpressionBoundaryResult DetectExpressionBoundaries(string lineContent, int startColumn, int endColumn)
    {
        try
        {
            _logger.LogDebug("Detecting expression boundaries in line: {LineContent}, columns {StartColumn}-{EndColumn}",
                lineContent, startColumn, endColumn);

            // Convert to 0-based indexing for easier processing
            var start0 = startColumn - 1;
            var end0 = endColumn - 1;

            // REF-001 FIX: Handle empty selection properly
            if (startColumn == endColumn)
            {
                return new ExpressionBoundaryResult
                {
                    Success = false,
                    ErrorMessage = "Selection is empty - start and end columns are the same"
                };
            }

            // Clamp to line bounds
            start0 = Math.Max(0, Math.Min(start0, lineContent.Length - 1));
            end0 = Math.Max(start0, Math.Min(end0, lineContent.Length - 1));

            // REF-001 FIX: Check for truly empty selections after clamping
            if (start0 >= end0)
            {
                return new ExpressionBoundaryResult
                {
                    Success = false,
                    ErrorMessage = "Selection results in empty range after boundary validation"
                };
            }

            // Try multiple strategies for boundary detection
            
            // SPECIAL CASE: Parentheses-specific detection (must come BEFORE AST)
            // This handles cases where the user specifically selects parentheses boundaries
            var parenthesesResult = TryDetectSpecificParenthesesSelection(lineContent, start0, end0);
            if (parenthesesResult.Success)
            {
                _logger.LogDebug("Boundary detection successful using parentheses method");
                return parenthesesResult;
            }

            // Strategy 1: AST-based parsing for the entire line
            var astResult = TryAstBasedBoundaryDetection(lineContent, start0, end0);
            if (astResult.Success)
            {
                _logger.LogDebug("Boundary detection successful using AST method");
                return astResult;
            }

            // Strategy 2: Pattern-based detection for common expressions
            var patternResult = TryPatternBasedBoundaryDetection(lineContent, start0, end0);
            if (patternResult.Success)
            {
                _logger.LogDebug("Boundary detection successful using pattern method");
                return patternResult;
            }

            // Strategy 3: Fallback cleanup (only if other strategies fail)
            _logger.LogDebug("Using fallback cleanup method for boundary detection");
            return CleanupOriginalSelection(lineContent, start0, end0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Boundary detection failed for line: {LineContent}", lineContent);
            return new ExpressionBoundaryResult
            {
                Success = false,
                ErrorMessage = $"Boundary detection failed: {ex.Message}"
            };
        }
    }
    
    /// <summary>
    /// SPECIAL CASE: Detect when user has specifically selected parentheses boundaries
    /// This must run before AST detection because AST doesn't preserve parentheses info
    /// </summary>
    private static ExpressionBoundaryResult TryDetectSpecificParenthesesSelection(string lineContent, int start0, int end0)
    {
        // ONLY activate parentheses detection when the user has DIRECTLY selected parentheses
        
        // User selected exactly from '(' to ')'
        if (start0 < lineContent.Length && end0 < lineContent.Length &&
            lineContent[start0] == '(' && lineContent[end0] == ')')
        {
            // Find the matching closing parenthesis using proper balancing
            var depth = 1;
            var matchingClose = -1;
            
            for (var i = start0 + 1; i < lineContent.Length; i++)
            {
                if (lineContent[i] == '(') depth++;
                else if (lineContent[i] == ')') 
                {
                    depth--;
                    if (depth == 0)
                    {
                        matchingClose = i;
                        break;
                    }
                }
            }
            
            // If the user's end selection matches the balanced closing parenthesis
            if (matchingClose == end0)
            {
                var parenthesesExpression = lineContent.Substring(start0, end0 - start0 + 1);
                return new ExpressionBoundaryResult
                {
                    Success = true,
                    Expression = parenthesesExpression.Trim(),
                    StartColumn = start0 + 1,  // Convert to 1-based
                    EndColumn = end0 + 1,      // Convert to 1-based
                    DetectionMethod = "Parentheses"
                };
            }
        }

        return new ExpressionBoundaryResult { Success = false };
    }

    /// <summary>
    /// REF-001 FIX: Pattern-based boundary detection for common TypeScript expressions
    /// </summary>
    private static ExpressionBoundaryResult TryPatternBasedBoundaryDetection(string lineContent, int start0, int end0)
    {
        // REF-001 FIX: Function call detection (handles Math.max, inject(), etc.)
        var functionCallResult = TryDetectFunctionCall(lineContent, start0, end0);
        if (functionCallResult.Success) return functionCallResult;

        // Property access detection (handles this.service.property)
        var propertyAccessResult = TryDetectPropertyAccess(lineContent, start0, end0);
        if (propertyAccessResult.Success) return propertyAccessResult;

        // Parentheses expression detection
        var parenthesesResult = TryDetectParenthesesExpression(lineContent, start0, end0);
        if (parenthesesResult.Success) return parenthesesResult;

        // String literal detection
        var stringLiteralResult = TryDetectStringLiteral(lineContent, start0, end0);
        if (stringLiteralResult.Success) return stringLiteralResult;

        return new ExpressionBoundaryResult { Success = false };
    }

    /// <summary>
    /// REF-001 FIX: Detect function calls and expand boundaries properly
    /// </summary>
    private static ExpressionBoundaryResult TryDetectFunctionCall(string lineContent, int start0, int end0)
    {
        // Look for function call patterns that might encompass or be near the selection
        var functionCallPattern = @"(\w+(?:\.\w+)*)\s*\(([^)]*)\)";
        var matches = Regex.Matches(lineContent, functionCallPattern);

        foreach (Match match in matches)
        {
            var matchStart = match.Index;
            var matchEnd = match.Index + match.Length;

            // REF-001 FIX: Check if selection overlaps with or is contained within this function call
            if (SelectionOverlapsOrIsNear(start0, end0, matchStart, matchEnd))
            {
                return new ExpressionBoundaryResult
                {
                    Success = true,
                    Expression = match.Value.Trim(),
                    StartColumn = matchStart + 1,  // Convert to 1-based
                    EndColumn = matchEnd,          // Regex end is exclusive
                    DetectionMethod = "FunctionCall"
                };
            }
        }

        return new ExpressionBoundaryResult { Success = false };
    }

    /// <summary>
    /// REF-001 FIX: Detect property access expressions
    /// </summary>
    private static ExpressionBoundaryResult TryDetectPropertyAccess(string lineContent, int start0, int end0)
    {
        // Property access pattern: word.word or this.word.word
        var propertyPattern = @"\b(\w+(?:\.\w+)+)";
        var matches = Regex.Matches(lineContent, propertyPattern);

        foreach (Match match in matches)
        {
            var matchStart = match.Index;
            var matchEnd = match.Index + match.Length;

            if (SelectionOverlapsOrIsNear(start0, end0, matchStart, matchEnd))
            {
                return new ExpressionBoundaryResult
                {
                    Success = true,
                    Expression = match.Value.Trim(),
                    StartColumn = matchStart + 1,
                    EndColumn = matchEnd,
                    DetectionMethod = "PropertyAccess"
                };
            }
        }

        return new ExpressionBoundaryResult { Success = false };
    }

    /// <summary>
    /// REF-001 FIX: Detect parentheses expressions with proper balancing
    /// </summary>
    private static ExpressionBoundaryResult TryDetectParenthesesExpression(string lineContent, int start0, int end0)
    {
        // Find the closest balanced parentheses that contain the selection
        for (var i = Math.Max(0, start0 - 10); i <= start0; i++)
        {
            if (i < lineContent.Length && lineContent[i] == '(')
            {
                // Find matching closing parenthesis
                var depth = 1;
                for (var j = i + 1; j < lineContent.Length; j++)
                {
                    if (lineContent[j] == '(') depth++;
                    else if (lineContent[j] == ')') depth--;
                    
                    if (depth == 0)
                    {
                        // Check if selection is within these parentheses
                        if (start0 >= i && end0 <= j)
                        {
                            var expression = lineContent.Substring(i, j - i + 1);
                            return new ExpressionBoundaryResult
                            {
                                Success = true,
                                Expression = expression.Trim(),
                                StartColumn = i + 1,
                                EndColumn = j + 1,
                                DetectionMethod = "Parentheses"
                            };
                        }
                        break;
                    }
                }
            }
        }

        return new ExpressionBoundaryResult { Success = false };
    }

    /// <summary>
    /// REF-001 FIX: Detect string literals (quotes and template literals)
    /// </summary>
    private static ExpressionBoundaryResult TryDetectStringLiteral(string lineContent, int start0, int end0)
    {
        // Check for string literals that encompass the selection
        var stringPatterns = new[]
        {
            (@"'([^']*)'", "StringLiteral"),
            (@"""([^""]*)""", "StringLiteral"),
            (@"`([^`]*)`", "StringLiteral")
        };

        foreach ((var pattern, var method) in stringPatterns)
        {
            var matches = Regex.Matches(lineContent, pattern);
            foreach (Match match in matches)
            {
                var matchStart = match.Index;
                var matchEnd = match.Index + match.Length;

                if (start0 >= matchStart && end0 <= matchEnd)
                {
                    return new ExpressionBoundaryResult
                    {
                        Success = true,
                        Expression = match.Value,
                        StartColumn = matchStart + 1,
                        EndColumn = matchEnd,
                        DetectionMethod = method
                    };
                }
            }
        }

        return new ExpressionBoundaryResult { Success = false };
    }

    /// <summary>
    /// DEFINITIVE: Find the most specific expression node that completely contains the selection
    /// No scoring, no heuristics - just pure AST logic
    /// </summary>
    private static Node? FindMostSpecificContainingExpressionNode(Script script, int start0, int end0)
    {
        var containingExpressionNodes = new List<(Node node, int size)>();

        void VisitNode(Node node)
        {
            if (node.Range == null) return;

            var nodeStart = node.Range.Start;
            var nodeEnd = node.Range.End;

            // DEFINITIVE RULE: Only consider nodes that completely contain the selection
            if (nodeStart <= start0 && nodeEnd >= end0)
            {
                // DEFINITIVE RULE: Only consider expression nodes, not statements
                if (IsExpressionNode(node))
                {
                    var size = nodeEnd - nodeStart;
                    containingExpressionNodes.Add((node, size));
                }
            }

            // Visit all children
            foreach (var child in node.ChildNodes)
            {
                VisitNode(child);
            }
        }

        // Visit the entire AST
        foreach (var statement in script.Body)
        {
            VisitNode(statement);
        }

        // DEFINITIVE ANSWER: Return the smallest containing expression node
        return containingExpressionNodes
            .OrderBy(x => x.size)
            .FirstOrDefault().node;
    }

    /// <summary>
    /// DEFINITIVE: Determine if a node represents an expression (not a statement)
    /// </summary>
    private static bool IsExpressionNode(Node node)
    {
        return node.Type switch
        {
            Nodes.CallExpression => true,        // function()
            Nodes.MemberExpression => true,      // obj.prop
            Nodes.BinaryExpression => true,      // a + b, a * b
            Nodes.UnaryExpression => true,       // !x, -x
            Nodes.AssignmentExpression => true,  // a = b
            Nodes.UpdateExpression => true,      // a++, ++a
            Nodes.ConditionalExpression => true, // a ? b : c
            Nodes.LogicalExpression => true,     // a && b, a || b
            Nodes.SequenceExpression => true,    // a, b
            Nodes.ThisExpression => true,        // this
            Nodes.ArrayExpression => true,       // [1, 2, 3]
            Nodes.ObjectExpression => true,      // {a: 1}
            Nodes.FunctionExpression => true,    // function() {}
            Nodes.ArrowFunctionExpression => true, // () => {}
            Nodes.NewExpression => true,         // new Foo()
            Nodes.Identifier => true,            // variableName
            Nodes.Literal => true,               // "string", 123, true
            Nodes.TemplateLiteral => true,       // `template ${string}`
            
            // DEFINITIVE: These are statements, not expressions
            Nodes.VariableDeclaration => false,  // const x = ...
            Nodes.ExpressionStatement => false,  // expression;
            Nodes.BlockStatement => false,       // { ... }
            Nodes.IfStatement => false,          // if (...) ...
            Nodes.ForStatement => false,         // for (...) ...
            Nodes.WhileStatement => false,       // while (...) ...
            Nodes.FunctionDeclaration => false,  // function name() {}
            Nodes.ReturnStatement => false,      // return ...
            
            _ => false
        };
    }

    /// <summary>
    /// DEFINITIVE: AST-based boundary detection with no scoring heuristics
    /// </summary>
    private static ExpressionBoundaryResult TryAstBasedBoundaryDetection(string lineContent, int start0, int end0)
    {
        try
        {
            var parser = new JavaScriptParser();
            var script = parser.ParseScript(lineContent);
        
            // DEFINITIVE: Find the most specific expression node that contains the selection
            var targetNode = FindMostSpecificContainingExpressionNode(script, start0, end0);
        
            if (targetNode?.Range != null)
            {
                var nodeText = GetNodeText(lineContent, targetNode);
            
                if (!string.IsNullOrWhiteSpace(nodeText))
                {
                    return new ExpressionBoundaryResult
                    {
                        Success = true,
                        Expression = nodeText,
                        StartColumn = targetNode.Range.Start + 1,  // Convert to 1-based
                        EndColumn = targetNode.Range.End,          // Esprima end is exclusive
                        DetectionMethod = GetDetectionMethod(targetNode)
                    };
                }
            }
        }
        catch (ParserException)
        {
            // AST parsing failed, will try pattern-based strategies
        }
    
        return new ExpressionBoundaryResult { Success = false };
    }

    /// <summary>
    /// REF-001 FIX: Check if selection overlaps with or is near a range (for expansion)
    /// </summary>
    private static bool SelectionOverlapsOrIsNear(int selStart, int selEnd, int rangeStart, int rangeEnd)
    {
        // Direct overlap
        if (SelectionOverlaps(selStart, selEnd, rangeStart, rangeEnd))
            return true;

        // Selection is contained within range
        if (rangeStart <= selStart && rangeEnd >= selEnd)
            return true;

        // Selection is very close to range (within 5 characters) - for expansion
        return Math.Abs(selStart - rangeEnd) <= 5 || Math.Abs(selEnd - rangeStart) <= 5;
    }

    /// <summary>
    /// Extract the source text for an AST node
    /// </summary>
    private static string GetNodeText(string source, Node node)
    {
        if (node.Range == null) return "";
        
        var start = Math.Max(0, node.Range.Start);
        var end = Math.Min(source.Length, node.Range.End);
        
        return start >= end ? "" : source.Substring(start, end - start).Trim();
    }

    /// <summary>
    /// Map AST node type to detection method string
    /// </summary>
    private static string GetDetectionMethod(Node node)
    {
        return node.Type switch
        {
            Nodes.CallExpression => "FunctionCall",
            Nodes.MemberExpression => "PropertyAccess", 
            Nodes.Literal => "StringLiteral",
            Nodes.TemplateLiteral => "StringLiteral",
            Nodes.ArrayExpression => "ArrayAccess",
            Nodes.ObjectExpression => "ObjectLiteral",
            Nodes.BinaryExpression => "ArithmeticExpression",
            Nodes.UnaryExpression => "ArithmeticExpression",
            Nodes.Identifier => "Identifier",
            _ => "Expression"
        };
    }

    /// <summary>
    /// REF-001 FIX: Improved fallback cleanup with better empty selection handling
    /// </summary>
    private static ExpressionBoundaryResult CleanupOriginalSelection(string lineContent, int start0, int end0)
    {
        // Clamp to valid bounds
        start0 = Math.Max(0, Math.Min(start0, lineContent.Length - 1));
        end0 = Math.Max(start0, Math.Min(end0, lineContent.Length - 1));

        if (start0 >= end0)
        {
            return new ExpressionBoundaryResult
            {
                Success = false,
                ErrorMessage = "Selection is empty after boundary adjustment"
            };
        }

        var selection = lineContent.Substring(start0, end0 - start0 + 1);
        var trimmed = selection.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return new ExpressionBoundaryResult
            {
                Success = false,
                ErrorMessage = "Selection is empty after trimming whitespace"
            };
        }

        // Find the trimmed boundaries
        var trimmedStart = lineContent.IndexOf(trimmed, start0, StringComparison.Ordinal);
        if (trimmedStart == -1) trimmedStart = start0;

        var trimmedEnd = trimmedStart + trimmed.Length - 1;

        return new ExpressionBoundaryResult
        {
            Success = true,
            Expression = trimmed,
            StartColumn = trimmedStart + 1,  // Convert to 1-based
            EndColumn = trimmedEnd + 1,      // Convert to 1-based
            DetectionMethod = "CleanupOriginal"
        };
    }

    /// <summary>
    /// Check if two ranges overlap
    /// </summary>
    private static bool SelectionOverlaps(int selStart, int selEnd, int rangeStart, int rangeEnd)
    {
        return !(selEnd < rangeStart || selStart > rangeEnd);
    }
}
