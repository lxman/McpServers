using McpCodeEditor.Interfaces;
using McpCodeEditor.Models;
using McpCodeEditor.Models.Refactoring.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System.Text.RegularExpressions;
using McpCodeEditor.Models.Refactoring;

namespace McpCodeEditor.Services.Refactoring.CSharp;

/// <summary>
/// Service for managing C# import (using statement) operations.
/// Handles organizing, adding, and analyzing using statements in C# files.
/// Implements the Single Responsibility Principle by focusing only on C# import management.
/// </summary>
public class CSharpImportManager(
    IPathValidationService pathValidationService,
    IBackupService backupService,
    IChangeTrackingService changeTrackingService)  // FIX: Use interface instead of concrete class
    : ICSharpImportManager
{
    /// <summary>
    /// Organize using statements in a C# file according to specified options.
    /// </summary>
    public async Task<RefactoringResult> OrganizeImportsAsync(
        string filePath,
        CSharpImportOperation options,
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = new RefactoringResult();

            // Resolve and validate file path
            string resolvedFilePath = pathValidationService.ValidateAndResolvePath(filePath);
            if (!File.Exists(resolvedFilePath))
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"File not found: {filePath}"
                };
            }

            // Validate it's a C# file
            if (!Path.GetExtension(resolvedFilePath).Equals(".cs", StringComparison.OrdinalIgnoreCase))
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"File is not a C# file: {filePath}"
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

            // Get current using directives
            SyntaxList<UsingDirectiveSyntax> currentUsings = root.Usings;
            if (!currentUsings.Any())
            {
                return new RefactoringResult
                {
                    Success = true,
                    Message = "No using statements found to organize",
                    FilesAffected = 0
                };
            }

            // Analyze and organize using statements
            CSharpImportAnalysis usingAnalysis = AnalyzeUsingStatements(currentUsings);
            IEnumerable<UsingDirectiveSyntax> organizedUsings = OrganizeUsingStatements(currentUsings, options);

            // Create new root with organized usings
            CompilationUnitSyntax newRoot = root.WithUsings(SyntaxFactory.List(organizedUsings));

            // Format the result
            var workspace = new AdhocWorkspace();
            SyntaxNode formattedRoot = Formatter.Format(newRoot, workspace, cancellationToken: cancellationToken);
            string modifiedContent = formattedRoot.ToFullString();

            // Check if there are actual changes
            if (sourceCode == modifiedContent)
            {
                return new RefactoringResult
                {
                    Success = true,
                    Message = "Using statements are already organized",
                    FilesAffected = 0,
                    Metadata =
                    {
                        ["usingCount"] = currentUsings.Count,
                        ["duplicatesRemoved"] = 0,
                        ["systemUsings"] = usingAnalysis.SystemUsings,
                        ["userUsings"] = usingAnalysis.UserUsings
                    }
                };
            }

            // Create backup if not preview
            string? backupId = null;
            if (!previewOnly)
            {
                backupId = await backupService.CreateBackupAsync(
                    Path.GetDirectoryName(resolvedFilePath)!,
                    "organize_imports");
            }

            var change = new FileChange
            {
                FilePath = resolvedFilePath,
                OriginalContent = sourceCode,
                ModifiedContent = modifiedContent,
                ChangeType = "OrganizeImports"
            };

            // Apply changes if not preview
            if (!previewOnly)
            {
                await File.WriteAllTextAsync(resolvedFilePath, modifiedContent, cancellationToken);

                await changeTrackingService.TrackChangeAsync(
                    resolvedFilePath,
                    sourceCode,
                    modifiedContent,
                    "Organize using statements",
                    backupId);
            }

            result.Success = true;
            result.Message = previewOnly
                ? $"Preview: Would organize {currentUsings.Count} using statements"
                : $"Successfully organized {currentUsings.Count} using statements";
            result.Changes = [change];
            result.FilesAffected = 1;
            result.Metadata["usingCount"] = currentUsings.Count;
            result.Metadata["backupId"] = backupId ?? "";
            result.Metadata["systemUsings"] = usingAnalysis.SystemUsings;
            result.Metadata["userUsings"] = usingAnalysis.UserUsings;
            result.Metadata["duplicatesRemoved"] = usingAnalysis.DuplicateUsings;

            return result;
        }
        catch (Exception ex)
        {
            return new RefactoringResult
            {
                Success = false,
                Error = $"Import organization failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Add a using statement to a C# file if it doesn't already exist.
    /// </summary>
    public async Task<RefactoringResult> AddUsingAsync(
        string filePath,
        string usingNamespace,
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = new RefactoringResult();

            // Validate namespace
            if (string.IsNullOrWhiteSpace(usingNamespace))
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = "Using namespace cannot be empty"
                };
            }

            // Resolve and validate file path
            string resolvedFilePath = pathValidationService.ValidateAndResolvePath(filePath);
            if (!File.Exists(resolvedFilePath))
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"File not found: {filePath}"
                };
            }

            // Validate it's a C# file
            if (!Path.GetExtension(resolvedFilePath).Equals(".cs", StringComparison.OrdinalIgnoreCase))
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"File is not a C# file: {filePath}"
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

            // Check if using already exists
            UsingDirectiveSyntax? existingUsing = root.Usings.FirstOrDefault(u =>
                u.Name?.ToString() == usingNamespace);

            if (existingUsing != null)
            {
                return new RefactoringResult
                {
                    Success = true,
                    Message = $"Using statement for '{usingNamespace}' already exists",
                    FilesAffected = 0
                };
            }

            // Validate namespace syntax
            if (!IsValidNamespace(usingNamespace))
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"Invalid namespace syntax: '{usingNamespace}'"
                };
            }

            // Create new using directive
            UsingDirectiveSyntax newUsing = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(usingNamespace));

            // Add to existing usings and sort
            SyntaxList<UsingDirectiveSyntax> newUsings = root.Usings.Add(newUsing);
            IEnumerable<UsingDirectiveSyntax> organizedUsings = OrganizeUsingStatements(newUsings, new CSharpImportOperation());
            
            CompilationUnitSyntax newRoot = root.WithUsings(SyntaxFactory.List(organizedUsings));

            // Format the result
            var workspace = new AdhocWorkspace();
            SyntaxNode formattedRoot = Formatter.Format(newRoot, workspace, cancellationToken: cancellationToken);
            string modifiedContent = formattedRoot.ToFullString();

            // Create backup if not preview
            string? backupId = null;
            if (!previewOnly)
            {
                backupId = await backupService.CreateBackupAsync(
                    Path.GetDirectoryName(resolvedFilePath)!,
                    $"add_using_{usingNamespace.Replace(".", "_")}");
            }

            var change = new FileChange
            {
                FilePath = resolvedFilePath,
                OriginalContent = sourceCode,
                ModifiedContent = modifiedContent,
                ChangeType = "AddUsing"
            };

            // Apply changes if not preview
            if (!previewOnly)
            {
                await File.WriteAllTextAsync(resolvedFilePath, modifiedContent, cancellationToken);

                await changeTrackingService.TrackChangeAsync(
                    resolvedFilePath,
                    sourceCode,
                    modifiedContent,
                    $"Add using statement: {usingNamespace}",
                    backupId);
            }

            result.Success = true;
            result.Message = previewOnly
                ? $"Preview: Would add using statement for '{usingNamespace}'"
                : $"Successfully added using statement for '{usingNamespace}'";
            result.Changes = [change];
            result.FilesAffected = 1;
            result.Metadata["usingNamespace"] = usingNamespace;
            result.Metadata["backupId"] = backupId ?? "";

            return result;
        }
        catch (Exception ex)
        {
            return new RefactoringResult
            {
                Success = false,
                Error = $"Add using failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Remove unused using statements from a C# file.
    /// This is a placeholder for future semantic analysis implementation.
    /// </summary>
    public async Task<RefactoringResult> RemoveUnusedImportsAsync(
        string filePath,
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement semantic analysis to detect unused using statements
        // This would require full compilation and semantic model analysis
        return new RefactoringResult
        {
            Success = false,
            Error = "Remove unused imports is not yet implemented. This requires semantic analysis."
        };
    }

    /// <summary>
    /// Get information about current using statements in a C# file.
    /// </summary>
    public async Task<CSharpImportAnalysis> AnalyzeImportsAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Resolve and validate file path
            string resolvedFilePath = pathValidationService.ValidateAndResolvePath(filePath);
            if (!File.Exists(resolvedFilePath))
            {
                throw new FileNotFoundException($"File not found: {filePath}");
            }

            // Validate it's a C# file
            if (!Path.GetExtension(resolvedFilePath).Equals(".cs", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"File is not a C# file: {filePath}");
            }

            string sourceCode = await File.ReadAllTextAsync(resolvedFilePath, cancellationToken);
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, cancellationToken: cancellationToken);

            if (await syntaxTree.GetRootAsync(cancellationToken) is not CompilationUnitSyntax root)
            {
                throw new InvalidOperationException("Failed to parse C# file");
            }

            // Analyze using statements
            return AnalyzeUsingStatements(root.Usings);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Import analysis failed: {ex.Message}", ex);
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Analyze the current using statements and provide insights.
    /// </summary>
    private static CSharpImportAnalysis AnalyzeUsingStatements(SyntaxList<UsingDirectiveSyntax> usings)
    {
        var analysis = new CSharpImportAnalysis();
        var seenNamespaces = new HashSet<string>();

        foreach ((UsingDirectiveSyntax usingDirective, int index) in usings.Select((u, i) => (u, i)))
        {
            string namespaceName = usingDirective.Name?.ToString() ?? "";
            
            var usingStatement = new CSharpUsingStatement
            {
                Namespace = namespaceName,
                LineNumber = index + 1, // Convert to 1-based
                IsSystemNamespace = IsSystemNamespace(namespaceName),
                OriginalText = usingDirective.ToFullString().Trim(),
                IsGlobal = usingDirective.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword),
                IsAlias = usingDirective.Alias != null,
                AliasName = usingDirective.Alias?.Name.ToString()
            };

            // Check for duplicates
            if (seenNamespaces.Contains(namespaceName))
            {
                usingStatement.IsDuplicate = true;
                analysis.DuplicateUsings++;
            }
            else
            {
                seenNamespaces.Add(namespaceName);
            }

            analysis.Usings.Add(usingStatement);

            if (usingStatement.IsSystemNamespace)
            {
                analysis.SystemUsings++;
            }
            else
            {
                analysis.UserUsings++;
            }
        }

        analysis.TotalUsings = usings.Count;

        // Generate suggestions
        if (analysis.DuplicateUsings > 0)
        {
            analysis.Issues.Add($"Found {analysis.DuplicateUsings} duplicate using statements");
            analysis.Suggestions.Add("Remove duplicate using statements");
        }

        if (!IsOrganized(usings))
        {
            analysis.Issues.Add("Using statements are not alphabetically organized");
            analysis.Suggestions.Add("Sort using statements alphabetically");
        }

        if (!AreSystemUsingsFirst(usings))
        {
            analysis.Issues.Add("System namespaces are not grouped at the top");
            analysis.Suggestions.Add("Group System namespaces before user namespaces");
        }

        return analysis;
    }

    /// <summary>
    /// Organize using statements according to the specified options.
    /// </summary>
    private static IEnumerable<UsingDirectiveSyntax> OrganizeUsingStatements(
        SyntaxList<UsingDirectiveSyntax> usings,
        CSharpImportOperation options)
    {
        List<UsingDirectiveSyntax> usingsList = usings.ToList();

        // Remove duplicates if requested
        if (options.RemoveDuplicates)
        {
            var seen = new HashSet<string>();
            usingsList = usingsList.Where(u =>
            {
                string namespaceName = u.Name?.ToString() ?? "";
                return seen.Add(namespaceName);
            }).ToList();
        }

        // Group by type if requested
        if (options.GroupByType || options.SeparateSystemNamespaces)
        {
            List<UsingDirectiveSyntax> systemUsings = usingsList.Where(u => IsSystemNamespace(u.Name?.ToString() ?? "")).ToList();
            List<UsingDirectiveSyntax> userUsings = usingsList.Where(u => !IsSystemNamespace(u.Name?.ToString() ?? "")).ToList();

            if (options.SortAlphabetically)
            {
                systemUsings = systemUsings.OrderBy(u => u.Name?.ToString() ?? "").ToList();
                userUsings = userUsings.OrderBy(u => u.Name?.ToString() ?? "").ToList();
            }

            return systemUsings.Concat(userUsings);
        }
        else if (options.SortAlphabetically)
        {
            return usingsList.OrderBy(u => u.Name?.ToString() ?? "");
        }

        return usingsList;
    }

    /// <summary>
    /// Check if a namespace is a System namespace.
    /// </summary>
    private static bool IsSystemNamespace(string namespaceName)
    {
        return namespaceName.StartsWith("System", StringComparison.Ordinal);
    }

    /// <summary>
    /// Check if using statements are already organized (sorted alphabetically).
    /// </summary>
    private static bool IsOrganized(SyntaxList<UsingDirectiveSyntax> usings)
    {
        if (usings.Count <= 1) return true;

        for (var i = 1; i < usings.Count; i++)
        {
            string current = usings[i].Name?.ToString() ?? "";
            string previous = usings[i - 1].Name?.ToString() ?? "";
            
            if (string.Compare(current, previous, StringComparison.Ordinal) < 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Check if System usings are grouped before user usings.
    /// </summary>
    private static bool AreSystemUsingsFirst(SyntaxList<UsingDirectiveSyntax> usings)
    {
        var foundUserUsing = false;
        
        foreach (UsingDirectiveSyntax usingDirective in usings)
        {
            string namespaceName = usingDirective.Name?.ToString() ?? "";
            bool isSystemNamespace = IsSystemNamespace(namespaceName);
            
            if (!isSystemNamespace)
            {
                foundUserUsing = true;
            }
            else if (foundUserUsing && isSystemNamespace)
            {
                // Found a System using after a user using
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Validate if a namespace string has valid syntax.
    /// </summary>
    private static bool IsValidNamespace(string namespaceName)
    {
        if (string.IsNullOrWhiteSpace(namespaceName))
            return false;

        // Check for valid namespace pattern (letters, dots, underscores)
        var namespacePattern = @"^[a-zA-Z_][a-zA-Z0-9_]*(\.[a-zA-Z_][a-zA-Z0-9_]*)*$";
        return Regex.IsMatch(namespaceName, namespacePattern);
    }

    #endregion
}
