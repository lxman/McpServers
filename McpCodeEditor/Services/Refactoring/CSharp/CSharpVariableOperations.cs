using McpCodeEditor.Interfaces;
using McpCodeEditor.Models;
using McpCodeEditor.Models.Refactoring;
using McpCodeEditor.Models.Refactoring.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace McpCodeEditor.Services.Refactoring.CSharp;

/// <summary>
/// Service for C# variable and field operations including introduction and encapsulation
/// </summary>
public class CSharpVariableOperations(
    IPathValidationService pathValidationService,
    IBackupService backupService,
    IChangeTrackingService changeTracking)  // FIXED: Changed from ChangeTrackingService to IChangeTrackingService
    : ICSharpVariableOperations
{
    /// <summary>
    /// Extract a selected expression into a local variable for better code readability
    /// </summary>
    public async Task<RefactoringResult> IntroduceVariableAsync(
        string filePath,
        VariableIntroductionOptions options,
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = new RefactoringResult();

            // Resolve relative paths to absolute paths
            string resolvedFilePath = pathValidationService.ValidateAndResolvePath(filePath);

            if (!File.Exists(resolvedFilePath))
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"File not found: {filePath}"
                };
            }

            string sourceCode = await File.ReadAllTextAsync(resolvedFilePath, cancellationToken);
            string[] lines = sourceCode.Split('\n');

            // Validate line number (1-based)
            if (options.Line < 1 || options.Line > lines.Length)
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"Invalid line number: {options.Line}. File has {lines.Length} lines."
                };
            }

            string selectedLine = lines[options.Line - 1]; // Convert to 0-based

            // Validate column range (1-based)
            if (options.StartColumn < 1 || options.EndColumn > selectedLine.Length || options.StartColumn > options.EndColumn)
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"Invalid column range: {options.StartColumn}-{options.EndColumn}. Line has {selectedLine.Length} characters."
                };
            }

            // Extract the selected expression (convert to 0-based indexing)
            string selectedExpression = selectedLine.Substring(options.StartColumn - 1, options.EndColumn - options.StartColumn + 1).Trim();

            if (string.IsNullOrWhiteSpace(selectedExpression))
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = "Selected expression is empty or whitespace"
                };
            }

            // Validate expression if requested
            if (options.ValidateExpression)
            {
                if (selectedExpression.Length < options.MinExpressionLength)
                {
                    return new RefactoringResult
                    {
                        Success = false,
                        Error = $"Selected expression is too short (minimum {options.MinExpressionLength} characters)"
                    };
                }

                if (selectedExpression.Length > options.MaxExpressionLength)
                {
                    return new RefactoringResult
                    {
                        Success = false,
                        Error = $"Selected expression is too long (maximum {options.MaxExpressionLength} characters)"
                    };
                }

                // Basic validation - ensure this looks like an expression
                if (selectedExpression.Contains(';') || selectedExpression.StartsWith('{') || selectedExpression.EndsWith('}'))
                {
                    return new RefactoringResult
                    {
                        Success = false,
                        Error = "Selected text does not appear to be a valid expression"
                    };
                }
            }

            // Generate variable name if not provided
            string variableName = options.VariableName ?? GenerateVariableName(selectedExpression);

            // Validate variable name
            if (!IsValidIdentifier(variableName))
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"Invalid variable name: '{variableName}'"
                };
            }

            // Get indentation from the current line or use custom
            string indentation = options.CustomIndentation ?? GetLineIndentation(selectedLine);

            // Determine variable type
            string variableType = options.VariableType;
            if (options.PreferConst && IsConstantExpression(selectedExpression))
            {
                variableType = "const var";
            }

            // Build the variable declaration
            var variableDeclaration = $"{indentation}{variableType} {variableName} = {selectedExpression};";

            // Add comment if requested
            if (options.AddComments)
            {
                var comment = $"{indentation}// Extracted from expression: {selectedExpression}";
                variableDeclaration = $"{comment}\n{variableDeclaration}";
            }

            // Replace the expression with the variable reference
            string beforeExpression = selectedLine[..(options.StartColumn - 1)];
            string afterExpression = selectedLine[options.EndColumn..];
            string modifiedLine = beforeExpression + variableName + afterExpression;

            // Build the modified content
            var modifiedLines = new List<string>();

            // Add lines before the target line
            for (var i = 0; i < options.Line - 1; i++)
            {
                modifiedLines.Add(lines[i]);
            }

            // Add the variable declaration
            modifiedLines.Add(variableDeclaration);

            // Add the modified line
            modifiedLines.Add(modifiedLine);

            // Add lines after the target line
            for (int i = options.Line; i < lines.Length; i++)
            {
                modifiedLines.Add(lines[i]);
            }

            string modifiedContent = string.Join("\n", modifiedLines);

            // Create backup if not preview
            string? backupId = null;
            if (!previewOnly)
            {
                backupId = await backupService.CreateBackupAsync(
                    Path.GetDirectoryName(resolvedFilePath)!,
                    $"introduce_variable_{variableName}");
            }

            var change = new FileChange
            {
                FilePath = resolvedFilePath,
                OriginalContent = sourceCode,
                ModifiedContent = modifiedContent,
                ChangeType = "IntroduceVariable"
            };

            // Apply changes if not preview
            if (!previewOnly)
            {
                await File.WriteAllTextAsync(resolvedFilePath, modifiedContent, cancellationToken);

                await changeTracking.TrackChangeAsync(
                    resolvedFilePath,
                    sourceCode,
                    modifiedContent,
                    $"Introduce variable '{variableName}' for expression '{selectedExpression}'",
                    backupId);
            }

            result.Success = true;
            result.Message = previewOnly
                ? $"Preview: Would introduce variable '{variableName}' for expression '{selectedExpression}'"
                : $"Successfully introduced variable '{variableName}' for expression '{selectedExpression}'";
            result.Changes = [change];
            result.FilesAffected = 1;
            result.Metadata["variableName"] = variableName;
            result.Metadata["expression"] = selectedExpression;
            result.Metadata["line"] = options.Line.ToString();
            result.Metadata["variableType"] = variableType;
            result.Metadata["backupId"] = backupId ?? "";

            return result;
        }
        catch (Exception ex)
        {
            return new RefactoringResult
            {
                Success = false,
                Error = $"Introduce variable failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Convert public fields to private fields with public properties for better encapsulation
    /// </summary>
    public async Task<RefactoringResult> EncapsulateFieldAsync(
        string filePath,
        FieldEncapsulationOptions options,
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = new RefactoringResult();

            // Resolve relative paths to absolute paths
            string resolvedFilePath = pathValidationService.ValidateAndResolvePath(filePath);

            if (!File.Exists(resolvedFilePath))
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"File not found: {filePath}"
                };
            }

            string sourceCode = await File.ReadAllTextAsync(resolvedFilePath, cancellationToken);
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, cancellationToken: cancellationToken);

            if (await syntaxTree.GetRootAsync(cancellationToken) is not CompilationUnitSyntax root)
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = "Failed to parse C# file"
                };
            }

            // Find the field to encapsulate
            FieldDeclarationSyntax? fieldDeclaration = null;
            VariableDeclaratorSyntax? targetVariable = null;

            foreach (FieldDeclarationSyntax field in root.DescendantNodes().OfType<FieldDeclarationSyntax>())
            {
                VariableDeclaratorSyntax? variable = field.Declaration.Variables.FirstOrDefault(v => v.Identifier.ValueText == options.FieldName);
                if (variable != null)
                {
                    fieldDeclaration = field;
                    targetVariable = variable;
                    break;
                }
            }

            if (fieldDeclaration == null || targetVariable == null)
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"Field '{options.FieldName}' not found in file"
                };
            }

            // Validate field access if requested
            if (options.ValidateFieldAccess)
            {
                bool isPublic = fieldDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));
                if (!isPublic)
                {
                    return new RefactoringResult
                    {
                        Success = false,
                        Error = $"Field '{options.FieldName}' is not public. Only public fields can be encapsulated."
                    };
                }
            }

            // Generate property name if not provided
            string propertyName = options.PropertyName ?? GeneratePropertyName(options.FieldName);

            // Validate property name
            if (!IsValidIdentifier(propertyName))
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"Invalid property name: '{propertyName}'"
                };
            }

            // Get field type
            string fieldType = fieldDeclaration.Declaration.Type.ToString().Trim();

            // Get field initializer if present
            var initializer = targetVariable.Initializer?.Value.ToString();

            // Find the class containing the field
            var containingClass = fieldDeclaration.FirstAncestorOrSelf<ClassDeclarationSyntax>();
            if (containingClass == null)
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = "Field is not contained within a class"
                };
            }

            CompilationUnitSyntax modifiedRoot = root;

            // Create private field name (with prefix)
            var privateFieldName = $"{options.BackingFieldPrefix}{options.FieldName.TrimStart('_')}";

            // Create property
            PropertyDeclarationSyntax newProperty;
            
            if (options.UseAutoProperty && (string.IsNullOrWhiteSpace(initializer) || !options.PreserveInitialization))
            {
                // Create auto-property
                var accessors = new List<AccessorDeclarationSyntax>();
                
                if (options.GenerateGetter)
                {
                    AccessorDeclarationSyntax getter = SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                        .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
                    
                    if (!string.IsNullOrWhiteSpace(options.GetterAccessModifier))
                    {
                        getter = getter.WithModifiers(SyntaxFactory.TokenList(
                            SyntaxFactory.ParseToken(options.GetterAccessModifier)));
                    }
                    accessors.Add(getter);
                }

                if (options.GenerateSetter)
                {
                    AccessorDeclarationSyntax setter = SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                        .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
                    
                    if (!string.IsNullOrWhiteSpace(options.SetterAccessModifier))
                    {
                        setter = setter.WithModifiers(SyntaxFactory.TokenList(
                            SyntaxFactory.ParseToken(options.SetterAccessModifier)));
                    }
                    accessors.Add(setter);
                }

                newProperty = SyntaxFactory.PropertyDeclaration(SyntaxFactory.ParseTypeName(fieldType), propertyName)
                    .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.ParseToken(options.PropertyAccessModifier)))
                    .WithAccessorList(SyntaxFactory.AccessorList(SyntaxFactory.List(accessors)))
                    .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

                // Replace the public field directly with the auto-property
                modifiedRoot = modifiedRoot.ReplaceNode(fieldDeclaration, newProperty);
            }
            else
            {
                // Create full property with getter and setter
                var accessors = new List<AccessorDeclarationSyntax>();

                if (options.GenerateGetter)
                {
                    AccessorDeclarationSyntax getter = SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                        .WithBody(SyntaxFactory.Block(
                            SyntaxFactory.ReturnStatement(
                                SyntaxFactory.IdentifierName(privateFieldName))));
                    
                    if (!string.IsNullOrWhiteSpace(options.GetterAccessModifier))
                    {
                        getter = getter.WithModifiers(SyntaxFactory.TokenList(
                            SyntaxFactory.ParseToken(options.GetterAccessModifier)));
                    }
                    accessors.Add(getter);
                }

                if (options.GenerateSetter)
                {
                    var setterBody = new List<StatementSyntax>();

                    // Add validation if requested
                    if (options.AddValidation && !string.IsNullOrWhiteSpace(options.ValidationCode))
                    {
                        setterBody.Add(SyntaxFactory.ParseStatement(options.ValidationCode));
                    }

                    setterBody.Add(SyntaxFactory.ExpressionStatement(
                        SyntaxFactory.AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            SyntaxFactory.IdentifierName(privateFieldName),
                            SyntaxFactory.IdentifierName("value"))));

                    AccessorDeclarationSyntax setter = SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                        .WithBody(SyntaxFactory.Block(setterBody));
                    
                    if (!string.IsNullOrWhiteSpace(options.SetterAccessModifier))
                    {
                        setter = setter.WithModifiers(SyntaxFactory.TokenList(
                            SyntaxFactory.ParseToken(options.SetterAccessModifier)));
                    }
                    accessors.Add(setter);
                }

                // Create property modifiers
                SyntaxTokenList propertyModifiers = SyntaxFactory.TokenList(SyntaxFactory.ParseToken(options.PropertyAccessModifier));
                if (options.MakeVirtual)
                {
                    propertyModifiers = propertyModifiers.Add(SyntaxFactory.Token(SyntaxKind.VirtualKeyword));
                }

                newProperty = SyntaxFactory.PropertyDeclaration(SyntaxFactory.ParseTypeName(fieldType), propertyName)
                    .WithModifiers(propertyModifiers)
                    .WithAccessorList(SyntaxFactory.AccessorList(SyntaxFactory.List(accessors)))
                    .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

                // Add documentation if requested
                if (options.AddDocumentation)
                {
                    string documentation = options.PropertyDocumentation ?? $"Gets or sets the {propertyName.ToLower()}.";
                    // TODO: Add XML documentation comments
                }

                // Create new private field
                FieldDeclarationSyntax newPrivateField = SyntaxFactory.FieldDeclaration(
                    SyntaxFactory.VariableDeclaration(SyntaxFactory.ParseTypeName(fieldType))
                        .WithVariables(SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.VariableDeclarator(privateFieldName)
                                .WithInitializer(targetVariable.Initializer))))
                    .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.ParseToken(options.BackingFieldAccessModifier)))
                    .WithLeadingTrivia(fieldDeclaration.GetLeadingTrivia())
                    .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

                // Get field index BEFORE replacing anything
                int fieldIndex = containingClass.Members.IndexOf(fieldDeclaration);

                // Replace the public field with private field
                modifiedRoot = modifiedRoot.ReplaceNode(fieldDeclaration, newPrivateField);

                // Now get the updated containing class from the modified root
                ClassDeclarationSyntax? updatedContainingClass = modifiedRoot.DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .FirstOrDefault(c => c.Identifier.ValueText == containingClass.Identifier.ValueText);

                if (updatedContainingClass == null)
                {
                    return new RefactoringResult
                    {
                        Success = false,
                        Error = "Failed to find containing class after field replacement"
                    };
                }

                // Add the property after the field using the original index
                SyntaxList<MemberDeclarationSyntax> newMembers = updatedContainingClass.Members.Insert(fieldIndex + 1, newProperty);
                ClassDeclarationSyntax newClass = updatedContainingClass.WithMembers(newMembers);
                modifiedRoot = modifiedRoot.ReplaceNode(updatedContainingClass, newClass);
            }

            // Update references to the old field name with the property name if requested
            if (options.UpdateReferences)
            {
                List<IdentifierNameSyntax> fieldReferences = modifiedRoot.DescendantNodes()
                    .OfType<IdentifierNameSyntax>()
                    .Where(id => id.Identifier.ValueText == options.FieldName)
                    .ToList();

                foreach (IdentifierNameSyntax reference in fieldReferences.Where(r => !IsInFieldDeclaration(r)))
                {
                    IdentifierNameSyntax newReference = SyntaxFactory.IdentifierName(propertyName);
                    modifiedRoot = modifiedRoot.ReplaceNode(reference, newReference);
                }
            }

            // Format the result
            var workspace = new AdhocWorkspace();
            SyntaxNode formattedRoot = Formatter.Format(modifiedRoot, workspace, cancellationToken: cancellationToken);
            string modifiedContent = formattedRoot.ToFullString();

            // Create backup if not preview
            string? backupId = null;
            if (!previewOnly)
            {
                backupId = await backupService.CreateBackupAsync(
                    Path.GetDirectoryName(resolvedFilePath)!,
                    $"encapsulate_field_{options.FieldName}");
            }

            var change = new FileChange
            {
                FilePath = resolvedFilePath,
                OriginalContent = sourceCode,
                ModifiedContent = modifiedContent,
                ChangeType = "EncapsulateField"
            };

            // Apply changes if not preview
            if (!previewOnly)
            {
                await File.WriteAllTextAsync(resolvedFilePath, modifiedContent, cancellationToken);

                string changeDescription = options.UseAutoProperty && string.IsNullOrWhiteSpace(initializer)
                    ? $"Encapsulate field '{options.FieldName}' as auto-property '{propertyName}'"
                    : $"Encapsulate field '{options.FieldName}' with property '{propertyName}' and private field '{privateFieldName}'";

                await changeTracking.TrackChangeAsync(
                    resolvedFilePath,
                    sourceCode,
                    modifiedContent,
                    changeDescription,
                    backupId);
            }

            string successMessage = options.UseAutoProperty && string.IsNullOrWhiteSpace(initializer)
                ? $"Successfully encapsulated field '{options.FieldName}' as auto-property '{propertyName}'"
                : $"Successfully encapsulated field '{options.FieldName}' with property '{propertyName}' and private field '{privateFieldName}'";

            result.Success = true;
            result.Message = previewOnly ? $"Preview: Would encapsulate field '{options.FieldName}'" : successMessage;
            result.Changes = [change];
            result.FilesAffected = 1;
            result.Metadata["fieldName"] = options.FieldName;
            result.Metadata["propertyName"] = propertyName;
            result.Metadata["privateFieldName"] = options.UseAutoProperty && string.IsNullOrWhiteSpace(initializer) ? "" : privateFieldName;
            result.Metadata["useAutoProperty"] = (options.UseAutoProperty && string.IsNullOrWhiteSpace(initializer)).ToString();
            result.Metadata["backupId"] = backupId ?? "";

            return result;
        }
        catch (Exception ex)
        {
            return new RefactoringResult
            {
                Success = false,
                Error = $"Field encapsulation failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Generate a meaningful variable name from an expression
    /// </summary>
    public string GenerateVariableName(string expression)
    {
        // Simple heuristics for generating variable names
        string cleaned = expression.Trim();

        // Handle method calls
        if (cleaned.Contains('(') && cleaned.Contains(')'))
        {
            string methodPart = cleaned[..cleaned.IndexOf('(')];
            string lastMethod = methodPart.Split('.').Last();
            return ToCamelCase($"{lastMethod}Result");
        }

        // Handle property access
        if (cleaned.Contains('.'))
        {
            string[] parts = cleaned.Split('.');
            if (parts.Length >= 2)
            {
                return ToCamelCase($"{parts[^2]}{parts[^1]}");
            }
        }

        // Handle arithmetic expressions
        if (cleaned.Contains('+') || cleaned.Contains('-') || cleaned.Contains('*') || cleaned.Contains('/'))
        {
            return "calculation";
        }

        // Handle string literals
        if (cleaned.StartsWith('"') && cleaned.EndsWith('"'))
        {
            return "text";
        }

        // Handle numeric literals
        if (int.TryParse(cleaned, out _) || double.TryParse(cleaned, out _))
        {
            return "value";
        }

        // Default fallback
        return "temp";
    }

    /// <summary>
    /// Generate a property name from a field name (PascalCase conversion)
    /// </summary>
    public string GeneratePropertyName(string fieldName)
    {
        string trimmed = fieldName.TrimStart('_');
        if (string.IsNullOrEmpty(trimmed))
            return "Property";

        return char.ToUpper(trimmed[0]) + trimmed[1..];
    }

    /// <summary>
    /// Validate if a string is a valid C# identifier
    /// </summary>
    public bool IsValidIdentifier(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        // Must start with letter or underscore
        if (!char.IsLetter(name[0]) && name[0] != '_')
            return false;

        // Rest must be letters, digits, or underscores
        for (var i = 1; i < name.Length; i++)
        {
            if (!char.IsLetterOrDigit(name[i]) && name[i] != '_')
                return false;
        }

        // Check against C# keywords
        string[] keywords =
        [
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
            "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
            "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
            "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
            "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
            "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this",
            "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort",
            "using", "virtual", "void", "volatile", "while", "var"
        ];

        return !keywords.Contains(name.ToLower());
    }

    #region Private Helper Methods

    /// <summary>
    /// Helper method to get the indentation of a line
    /// </summary>
    private static string GetLineIndentation(string line)
    {
        var indentEnd = 0;
        while (indentEnd < line.Length && char.IsWhiteSpace(line[indentEnd]))
        {
            indentEnd++;
        }
        return line[..indentEnd];
    }

    /// <summary>
    /// Convert string to camelCase
    /// </summary>
    private static string ToCamelCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "temp";

        string result = input.Trim();
        if (result.Length == 0)
            return "temp";

        // Remove non-alphanumeric characters and capitalize next letter
        var chars = new List<char>();
        var capitalizeNext = false;

        for (var i = 0; i < result.Length; i++)
        {
            char c = result[i];
            if (char.IsLetterOrDigit(c))
            {
                if (capitalizeNext && char.IsLetter(c))
                {
                    chars.Add(char.ToUpper(c));
                    capitalizeNext = false;
                }
                else if (chars.Count == 0 && char.IsLetter(c))
                {
                    chars.Add(char.ToLower(c)); // First letter lowercase for camelCase
                }
                else
                {
                    chars.Add(c);
                }
            }
            else
            {
                capitalizeNext = true;
            }
        }

        var final = new string(chars.ToArray());
        return string.IsNullOrEmpty(final) ? "temp" : final;
    }

    /// <summary>
    /// Check if an expression appears to be a constant
    /// </summary>
    private static bool IsConstantExpression(string expression)
    {
        string cleaned = expression.Trim();
        
        // Check for string literals
        if ((cleaned.StartsWith('"') && cleaned.EndsWith('"')) ||
            (cleaned.StartsWith('\'') && cleaned.EndsWith('\'')))
            return true;

        // Check for numeric literals
        if (int.TryParse(cleaned, out _) || 
            double.TryParse(cleaned, out _) || 
            decimal.TryParse(cleaned, out _))
            return true;

        // Check for boolean literals
        if (cleaned.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            cleaned.Equals("false", StringComparison.OrdinalIgnoreCase))
            return true;

        // Check for null
        if (cleaned.Equals("null", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>
    /// Check if an identifier is within a field declaration (to avoid replacing field names in their own declarations)
    /// </summary>
    private static bool IsInFieldDeclaration(IdentifierNameSyntax identifier)
    {
        return identifier.FirstAncestorOrSelf<FieldDeclarationSyntax>() != null;
    }

    #endregion
}
