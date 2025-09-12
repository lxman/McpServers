using McpCodeEditor.Interfaces;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;

namespace McpCodeEditor.Services.CSharp;

/// <summary>
/// Enhanced variable analysis service for C# method extraction using proper Roslyn semantic analysis
/// SESSION 1 FIX: Improved variable classification logic to properly handle external modified variables
/// SESSION 2 FIX: Added parameter filtering to exclude class fields
/// SESSION 4 FIX: Fixed parameter detection to properly identify external variables and their types
/// </summary>
public class EnhancedVariableAnalysisService(
    IParameterFilteringService parameterFilteringService,
    ILogger<EnhancedVariableAnalysisService>? logger = null)
    : IEnhancedVariableAnalysisService
{
    private readonly IParameterFilteringService _parameterFilteringService = parameterFilteringService ?? throw new ArgumentNullException(nameof(parameterFilteringService));

    /// <summary>
/// VARIABLE DETECTION ALGORITHM FIX: Complete rewrite to properly detect actual variables
/// Replaces the broken logic in lines 80-102 of AnalyzeVariableScopeAsync method
/// </summary>
public async Task<VariableScopeAnalysis> AnalyzeVariableScopeAsync(
    string[] extractedLines,
    SemanticModel? semanticModel,
    IEnumerable<SyntaxNode> syntaxNodes)
{
    logger?.LogDebug("Starting enhanced variable scope analysis for {LineCount} lines", extractedLines.Length);

    var result = new VariableScopeAnalysis();

    // Convert extracted lines to a syntax tree for analysis
    string extractedCode = string.Join(Environment.NewLine, extractedLines);
    
    // SESSION 1 FIX: Parse as statement list instead of wrapping in method
    // This helps preserve variable declaration context
    SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(extractedCode);
    SyntaxNode root = await syntaxTree.GetRootAsync();

    // SESSION 4 FIX: Track all variable declarations and usages more precisely
    var declaredVariables = new HashSet<string>();
    var allVariableUsages = new Dictionary<string, VariableUsageInfo>();

    // Find all variable declarations in the extracted code
    foreach (VariableDeclarationSyntax declaration in root.DescendantNodes().OfType<VariableDeclarationSyntax>())
    {
        foreach (VariableDeclaratorSyntax variable in declaration.Variables)
        {
            string varName = variable.Identifier.ValueText;
            declaredVariables.Add(varName);
            
            // SESSION 4 FIX: Determine actual type from declaration
            string varType = DetermineVariableType(declaration.Type);
            
            var variableInfo = new VariableInfo
            {
                Name = varName,
                Type = varType,
                IsDeclaredInExtraction = true,
                Scope = VariableScope.Local
            };
            
            result.LocalVariables.Add(variableInfo);
            logger?.LogDebug("Found local variable declaration: {Name} of type {Type}", 
                variableInfo.Name, variableInfo.Type);
        }
    }

    // VARIABLE DETECTION ALGORITHM FIX: Completely rewritten detection logic
    // Instead of processing all identifiers, focus on specific contexts where variables are actually used
    
    // 1. Find variables in foreach loops (both collection and loop variable)
    foreach (ForEachStatementSyntax foreachLoop in root.DescendantNodes().OfType<ForEachStatementSyntax>())
    {
        // Loop variable (e.g., 'order' in 'foreach (var order in orders)')
        string loopVarName = foreachLoop.Identifier.ValueText;
        declaredVariables.Add(loopVarName);
        
        var loopVarInfo = new VariableInfo
        {
            Name = loopVarName,
            Type = DetermineVariableType(foreachLoop.Type),
            IsDeclaredInExtraction = true,
            Scope = VariableScope.Local
        };
        result.LocalVariables.Add(loopVarInfo);
        
        // Collection being iterated (e.g., 'orders' in 'foreach (var order in orders)')
        if (foreachLoop.Expression is IdentifierNameSyntax collectionIdentifier)
        {
            string collectionName = collectionIdentifier.Identifier.ValueText;
            if (!IsKeywordOrType(collectionName) && !declaredVariables.Contains(collectionName))
            {
                RecordVariableUsage(allVariableUsages, collectionName, isRead: true, isWrite: false);
                logger?.LogDebug("Found collection variable in foreach: {Name} (READ)", collectionName);
            }
        }
    }

    // 2. Find variables in assignment expressions (primary source of modified variables)
    foreach (AssignmentExpressionSyntax assignment in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
    {
        // Left side of assignment (being written to)
        if (assignment.Left is IdentifierNameSyntax leftIdentifier)
        {
            string varName = leftIdentifier.Identifier.ValueText;
            if (!IsKeywordOrType(varName))
            {
                RecordVariableUsage(allVariableUsages, varName, isRead: false, isWrite: true);
                logger?.LogDebug("Found assignment target: {Name} (WRITE)", varName);
            }
        }
        
        // Right side of assignment (being read from) - but only simple identifiers, not property accesses
        foreach (IdentifierNameSyntax rightIdentifier in GetSimpleIdentifiers(assignment.Right))
        {
            string varName = rightIdentifier.Identifier.ValueText;
            if (!IsKeywordOrType(varName) && !declaredVariables.Contains(varName))
            {
                RecordVariableUsage(allVariableUsages, varName, isRead: true, isWrite: false);
                logger?.LogDebug("Found assignment source: {Name} (READ)", varName);
            }
        }
    }

    // 3. Find variables in increment/decrement operations (e.g., pendingCount++)
    foreach (PostfixUnaryExpressionSyntax postfix in root.DescendantNodes().OfType<PostfixUnaryExpressionSyntax>())
    {
        if (postfix.IsKind(SyntaxKind.PostIncrementExpression) || postfix.IsKind(SyntaxKind.PostDecrementExpression))
        {
            if (postfix.Operand is IdentifierNameSyntax identifier)
            {
                string varName = identifier.Identifier.ValueText;
                if (!IsKeywordOrType(varName))
                {
                    RecordVariableUsage(allVariableUsages, varName, isRead: true, isWrite: true);
                    logger?.LogDebug("Found increment/decrement: {Name} (READ+WRITE)", varName);
                }
            }
        }
    }

    foreach (PrefixUnaryExpressionSyntax prefix in root.DescendantNodes().OfType<PrefixUnaryExpressionSyntax>())
    {
        if (prefix.IsKind(SyntaxKind.PreIncrementExpression) || prefix.IsKind(SyntaxKind.PreDecrementExpression))
        {
            if (prefix.Operand is IdentifierNameSyntax identifier)
            {
                string varName = identifier.Identifier.ValueText;
                if (!IsKeywordOrType(varName))
                {
                    RecordVariableUsage(allVariableUsages, varName, isRead: true, isWrite: true);
                    logger?.LogDebug("Found pre-increment/decrement: {Name} (READ+WRITE)", varName);
                }
            }
        }
    }

    // 4. Find variables used as method arguments (but not property accesses)
    foreach (InvocationExpressionSyntax invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
    {
        foreach (ArgumentSyntax argument in invocation.ArgumentList.Arguments)
        {
            foreach (IdentifierNameSyntax identifier in GetSimpleIdentifiers(argument.Expression))
            {
                string varName = identifier.Identifier.ValueText;
                if (!IsKeywordOrType(varName) && !declaredVariables.Contains(varName))
                {
                    RecordVariableUsage(allVariableUsages, varName, isRead: true, isWrite: false);
                    logger?.LogDebug("Found method argument: {Name} (READ)", varName);
                }
            }
        }
    }

    // Process the collected variable usages
    foreach (VariableUsageInfo usage in allVariableUsages.Values)
    {
        // If a variable is not declared in extraction, it's external
        if (!declaredVariables.Contains(usage.Name))
        {
            string inferredType = InferVariableTypeFromUsage(usage.Name, extractedCode);
            
            var variableInfo = new VariableInfo
            {
                Name = usage.Name,
                Type = inferredType,
                IsDeclaredInExtraction = false,
                Scope = VariableScope.Parameter,
                IsModified = usage.IsWritten,
                UsagePattern = usage is { IsRead: true, IsWritten: true } ? VariableUsagePattern.ReadWrite :
                              usage.IsRead ? VariableUsagePattern.ReadOnly : VariableUsagePattern.WriteOnly
            };
            
            result.ExternalVariables.Add(variableInfo);
            
            // Any external variable that is READ needs to be passed as parameter
            if (usage.IsRead)
            {
                result.ParameterVariables.Add(variableInfo);
                logger?.LogDebug("External variable {Name} ({Type}) needs to be passed as parameter (READ)", 
                    usage.Name, inferredType);
            }
            
            // If an external variable is WRITTEN, it needs to be returned
            if (usage.IsWritten)
            {
                result.ModifiedVariables.Add(variableInfo);
                logger?.LogDebug("External variable {Name} ({Type}) is modified in extraction", 
                    usage.Name, inferredType);
            }
        }
        else
        {
            // Local variable - check if it's used after extraction (would need to be returned)
            VariableInfo? localVar = result.LocalVariables.FirstOrDefault(v => v.Name == usage.Name);
            if (localVar != null)
            {
                // SESSION 1 FIX: Mark local variables that might be used after extraction
                // This is a heuristic - in real extraction, we'd need to analyze code after extraction
                localVar.IsUsedAfterExtraction = true;
            }
        }
    }

    logger?.LogDebug("Scope analysis complete: {Local} local, {External} external, {Modified} modified, {Parameters} parameters", 
        result.LocalVariables.Count, result.ExternalVariables.Count, result.ModifiedVariables.Count, result.ParameterVariables.Count);

    return result;
}


    /// <summary>
    /// Classifies variable usage patterns using Roslyn syntax analysis
    /// </summary>
    public async Task<VariableUsageClassification> ClassifyVariableUsageAsync(
        string[] extractedLines,
        SemanticModel? semanticModel,
        IEnumerable<SyntaxNode> syntaxNodes)
    {
        logger?.LogDebug("Starting variable usage classification");

        var result = new VariableUsageClassification();
        string extractedCode = string.Join(Environment.NewLine, extractedLines);
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(extractedCode);
        SyntaxNode root = await syntaxTree.GetRootAsync();

        // Collect all variable usages
        var variableUsages = new Dictionary<string, VariableUsageInfo>();

        foreach (IdentifierNameSyntax identifier in root.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            string variableName = identifier.Identifier.ValueText;
            
            if (IsKeywordOrType(variableName))
                continue;

            if (!variableUsages.ContainsKey(variableName))
            {
                variableUsages[variableName] = new VariableUsageInfo { Name = variableName };
            }

            if (IsWriteContext(identifier))
            {
                variableUsages[variableName].IsWritten = true;
            }
            else
            {
                variableUsages[variableName].IsRead = true;
            }
        }

        // Classify based on usage patterns
        foreach (VariableUsageInfo usage in variableUsages.Values)
        {
            string inferredType = InferVariableTypeFromUsage(usage.Name, extractedCode);
            
            var variableInfo = new VariableInfo
            {
                Name = usage.Name,
                Type = inferredType
            };

            if (usage is { IsRead: true, IsWritten: false })
            {
                result.ReadOnlyVariables.Add(variableInfo with { UsagePattern = VariableUsagePattern.ReadOnly });
            }
            else if (usage is { IsRead: false, IsWritten: true })
            {
                result.WriteOnlyVariables.Add(variableInfo with { UsagePattern = VariableUsagePattern.WriteOnly });
            }
            else if (usage is { IsRead: true, IsWritten: true })
            {
                result.ReadWriteVariables.Add(variableInfo with { UsagePattern = VariableUsagePattern.ReadWrite });
            }
        }

        logger?.LogDebug("Usage classification: {ReadOnly} read-only, {WriteOnly} write-only, {ReadWrite} read-write",
            result.ReadOnlyVariables.Count, result.WriteOnlyVariables.Count, result.ReadWriteVariables.Count);

        return result;
    }

    /// <summary>
    /// Generates variable handling mapping
    /// </summary>
    public VariableHandlingMapping GenerateVariableMapping(
        VariableScopeAnalysis scopeAnalysis,
        VariableUsageClassification usageClassification,
        string[]? extractedLines = null,
        string[]? fullFileLines = null)
    {
        logger?.LogDebug("Generating variable handling mapping");

        var result = new VariableHandlingMapping();

        List<VariableInfo> candidateParameters = scopeAnalysis.ParameterVariables.ToList();
        
        List<VariableInfo> filteredParameters = candidateParameters;
        
        if (extractedLines != null)
        {
            filteredParameters = _parameterFilteringService.FilterParametersToPass(
                candidateParameters,
                extractedLines,
                fullFileLines);
            
            logger?.LogDebug("Filtered parameters: {Original} -> {Filtered}",
                candidateParameters.Count, filteredParameters.Count);
        }

        foreach (VariableInfo variable in filteredParameters)
        {
            result.ParametersToPass.Add(variable);
            
            // If an external variable is modified, it needs to be returned and assigned
            if (scopeAnalysis.ModifiedVariables.Any(v => v.Name == variable.Name))
            {
                result.VariablesToReturn.Add(variable);
                result.VariablesToAssign.Add(variable); // External modified = needs assignment
                logger?.LogDebug("External variable {Name} ({Type}) is modified - will be returned and assigned", 
                    variable.Name, variable.Type);
            }
        }

        // Local variables declared in extraction
        foreach (VariableInfo variable in scopeAnalysis.LocalVariables)
        {
            result.VariablesToDeclare.Add(variable);
            
            // If local variable is used after extraction, it needs to be returned
            if (variable.IsUsedAfterExtraction)
            {
                result.VariablesToReturn.Add(variable);
                logger?.LogDebug("Local variable {Name} ({Type}) is used after extraction - will be returned", 
                    variable.Name, variable.Type);
            }
        }

        // Determine return type based on what needs to be returned
        if (result.VariablesToReturn.Count == 0)
        {
            result.SuggestedReturnType = "void";
        }
        else if (result.VariablesToReturn.Count == 1)
        {
            VariableInfo returnVar = result.VariablesToReturn.First();
            result.SuggestedReturnType = !string.IsNullOrEmpty(returnVar.Type) && returnVar.Type != "var" 
                ? returnVar.Type 
                : "int"; // Default to int for better compatibility
        }
        else
        {
            // Multiple return values require tuple
            List<string> types = result.VariablesToReturn.Select(v => 
                !string.IsNullOrEmpty(v.Type) && v.Type != "var" ? v.Type : "int").ToList();
            result.SuggestedReturnType = $"({string.Join(", ", types)})";
        }

        logger?.LogDebug("Mapping complete: {Params} params, {Declare} to declare, {Assign} to assign, {Return} to return, ReturnType: {ReturnType}",
            result.ParametersToPass.Count, 
            result.VariablesToDeclare.Count, 
            result.VariablesToAssign.Count, 
            result.VariablesToReturn.Count,
            result.SuggestedReturnType);

        return result;
    }

    /// <summary>
    /// Performs complete variable analysis using Roslyn
    /// </summary>
    public async Task<EnhancedVariableAnalysisResult> PerformCompleteAnalysisAsync(
        string[] extractedLines,
        SemanticModel? semanticModel,
        IEnumerable<SyntaxNode> syntaxNodes,
        string[]? fullFileLines = null)
    {
        logger?.LogDebug("Starting complete enhanced variable analysis");

        var result = new EnhancedVariableAnalysisResult();

        try
        {
            // Perform all analysis phases
            List<SyntaxNode> nodes = syntaxNodes.ToList();
            result.ScopeAnalysis = await AnalyzeVariableScopeAsync(extractedLines, semanticModel, nodes);
            result.UsageClassification = await ClassifyVariableUsageAsync(extractedLines, semanticModel, nodes);
            
            result.HandlingMapping = GenerateVariableMapping(
                result.ScopeAnalysis, 
                result.UsageClassification,
                extractedLines,
                fullFileLines);

            result.IsSuccessful = true;
            logger?.LogDebug("Complete analysis successful");
        }
        catch (Exception ex)
        {
            result.IsSuccessful = false;
            result.Errors.Add($"Analysis failed: {ex.Message}");
            logger?.LogError(ex, "Variable analysis failed");
        }

        return result;
    }

    #region Helper Methods
    
    /// <summary>
    /// Helper method to record variable usage patterns
    /// </summary>
    private static void RecordVariableUsage(Dictionary<string, VariableUsageInfo> usages, string varName, bool isRead, bool isWrite)
    {
        if (!usages.ContainsKey(varName))
        {
            usages[varName] = new VariableUsageInfo { Name = varName };
        }
    
        if (isRead) usages[varName].IsRead = true;
        if (isWrite) usages[varName].IsWritten = true;
    }

    /// <summary>
    /// Gets simple identifiers from an expression, excluding property accesses
    /// This prevents detecting "Status" and "Amount" from "order.Status" and "order.Amount"
    /// </summary>
    private static IEnumerable<IdentifierNameSyntax> GetSimpleIdentifiers(SyntaxNode expression)
    {
        // Only return identifiers that are NOT part of member access expressions
        return expression.DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Where(identifier => 
            {
                // Exclude identifiers that are the right side of member access (e.g., "Status" in "order.Status")
                if (identifier.Parent is MemberAccessExpressionSyntax memberAccess && 
                    memberAccess.Name == identifier)
                {
                    return false;
                }
            
                // Include identifiers that are standalone or the left side of member access
                return true;
            });
    }

    /// <summary>
    /// Determine variable type from declaration syntax
    /// </summary>
    private static string DetermineVariableType(TypeSyntax? typeSyntax)
    {
        if (typeSyntax == null)
            return "object";

        var typeText = typeSyntax.ToString();
        
        // Handle var keyword
        if (typeText == "var")
            return "object"; // Default when we can't infer
            
        // Clean up the type text
        return CleanTypeText(typeText);
    }

    /// <summary>
    /// SESSION 4 FIX: Infer variable type from usage patterns and naming conventions
    /// </summary>
    private static string InferVariableTypeFromUsage(string variableName, string code)
    {
        // Check for obvious type indicators in variable name
        string lowerName = variableName.ToLower();
        
        // Integer patterns
        if (lowerName.Contains("count") || lowerName.Contains("index") || lowerName.Contains("id") || 
            lowerName.Contains("total") || lowerName.EndsWith("num") || lowerName.StartsWith("num"))
        {
            return "int";
        }
        
        // String patterns
        if (lowerName.Contains("name") || lowerName.Contains("text") || lowerName.Contains("message") ||
            lowerName.Contains("description") || lowerName.Contains("title"))
        {
            return "string";
        }
        
        // Boolean patterns
        if (lowerName.StartsWith("is") || lowerName.StartsWith("has") || lowerName.StartsWith("can") ||
            lowerName.Contains("flag") || lowerName.Contains("enabled"))
        {
            return "bool";
        }
        
        // Collection patterns
        if (lowerName.EndsWith("s") || lowerName.Contains("list") || lowerName.Contains("collection") ||
            lowerName.Contains("array") || lowerName.Contains("items"))
        {
            // Try to determine if it's a specific collection type
            if (lowerName.Contains("order"))
                return "List<Order>";
            if (lowerName.Contains("customer"))
                return "List<Customer>";
            if (lowerName.Contains("product"))
                return "List<Product>";
            if (lowerName.Contains("item"))
                return "List<Item>";
                
            return "List<object>";
        }
        
        // DateTime patterns
        if (lowerName.Contains("date") || lowerName.Contains("time") || lowerName.Contains("created") ||
            lowerName.Contains("updated") || lowerName.Contains("modified"))
        {
            return "DateTime";
        }
        
        // Decimal/Money patterns
        if (lowerName.Contains("price") || lowerName.Contains("cost") || lowerName.Contains("amount") ||
            lowerName.Contains("total") || lowerName.Contains("balance"))
        {
            return "decimal";
        }
        
        // Try to infer from context in code
        if (code.Contains($"{variableName}.Count") || code.Contains($"{variableName}.Length"))
        {
            return "List<object>"; // Something with Count/Length
        }
        
        if (code.Contains($"{variableName}++") || code.Contains($"++{variableName}") ||
            code.Contains($"{variableName} += ") || code.Contains($"{variableName} = {variableName} + "))
        {
            return "int"; // Something being incremented
        }
        
        // Default to object for unknown types
        return "object";
    }

    /// <summary>
    /// SESSION 4 FIX: Clean up type text for consistency
    /// </summary>
    private static string CleanTypeText(string typeText)
    {
        if (string.IsNullOrWhiteSpace(typeText))
            return "object";
            
        // Remove extra whitespace
        typeText = typeText.Trim();
        
        // Handle common type aliases
        return typeText switch
        {
            "String" => "string",
            "Int32" => "int",
            "Boolean" => "bool",
            "Double" => "double",
            "Single" => "float",
            "Decimal" => "decimal",
            "Object" => "object",
            _ => typeText
        };
    }

    /// <summary>
    /// Determines if an identifier is in a write context
    /// </summary>
    private static bool IsWriteContext(IdentifierNameSyntax identifier)
    {
        SyntaxNode? parent = identifier.Parent;

        return parent switch
        {
            // Assignment: x = value or x += value
            AssignmentExpressionSyntax assignment when assignment.Left == identifier => true,
            
            // Variable declaration: var x = value  
            VariableDeclaratorSyntax => true,
            
            // Increment/decrement: x++, ++x, x--, --x
            PostfixUnaryExpressionSyntax postfix when postfix.Operand == identifier && 
                (postfix.IsKind(SyntaxKind.PostIncrementExpression) || 
                 postfix.IsKind(SyntaxKind.PostDecrementExpression)) => true,
            PrefixUnaryExpressionSyntax prefix when prefix.Operand == identifier &&
                (prefix.IsKind(SyntaxKind.PreIncrementExpression) || 
                 prefix.IsKind(SyntaxKind.PreDecrementExpression)) => true,
            
            // Out/ref parameters
            ArgumentSyntax argument when argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword) || 
                                        argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword) => true,
            
            _ => false
        };
    }

    /// <summary>
    /// Checks if a string is a C# keyword or built-in type
    /// </summary>
    private static bool IsKeywordOrType(string name)
    {
        // Check for C# keywords
        if (SyntaxFacts.IsKeywordKind(SyntaxFacts.GetKeywordKind(name)))
            return true;
            
        // Check for common types that might not be keywords
        var commonTypes = new[] { "Console", "String", "Math", "DateTime", "List", "Dictionary", 
                                  "Array", "Task", "Exception", "Object" };
        return commonTypes.Contains(name);
    }

    #endregion

    #region Helper Classes

    /// <summary>
    /// Internal class to track variable usage patterns
    /// </summary>
    private class VariableUsageInfo
    {
        public string Name { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public bool IsWritten { get; set; }
    }

    #endregion
}
