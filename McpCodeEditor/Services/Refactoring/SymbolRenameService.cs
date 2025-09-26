using McpCodeEditor.Interfaces;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.MSBuild;
using McpCodeEditor.Models;
using McpCodeEditor.Models.Refactoring;
using Microsoft.CodeAnalysis.Text;

namespace McpCodeEditor.Services.Refactoring;

/// <summary>
/// Service responsible for symbol renaming operations across C# and TypeScript projects - ENHANCED VERSION
/// Now supports both C# (using Roslyn) and TypeScript (using regex-based analysis)
/// </summary>
public class SymbolRenameService(
    CodeEditorConfigurationService config,
    IBackupService backupService,
    IChangeTrackingService changeTracking,
    TypeScriptSymbolRenameService typeScriptRenameService)
{
    /// <summary>
    /// Rename a symbol across all files in the project - ENHANCED VERSION supporting C# and TypeScript
    /// </summary>
    public async Task<RefactoringResult> RenameSymbolAsync(
        string symbolName,
        string newName,
        string? filePath = null,
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Determine the language(s) to process
            LanguageScopeInfo languageInfo = DetermineLanguageScope(filePath);

            if (languageInfo is { HasTypeScript: true, HasCSharp: true })
            {
                // Handle mixed language scenario
                return await HandleMixedLanguageRenameAsync(symbolName, newName, filePath, previewOnly, cancellationToken);
            }
            else if (languageInfo.HasTypeScript)
            {
                // Pure TypeScript scenario
                return await typeScriptRenameService.RenameSymbolAsync(symbolName, newName, filePath, previewOnly, cancellationToken);
            }
            else
            {
                // Pure C# scenario (existing logic)
                return await RenameSymbolCSharpAsync(symbolName, newName, filePath, previewOnly, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            return new RefactoringResult
            {
                Success = false,
                Error = $"Symbol rename failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Handle symbol renaming across both C# and TypeScript files
    /// </summary>
    private async Task<RefactoringResult> HandleMixedLanguageRenameAsync(
        string symbolName,
        string newName,
        string? filePath,
        bool previewOnly,
        CancellationToken cancellationToken)
    {
        // Execute both C# and TypeScript renaming
        RefactoringResult csharpResult = await RenameSymbolCSharpAsync(symbolName, newName, filePath, previewOnly, cancellationToken);
        RefactoringResult typeScriptResult = await typeScriptRenameService.RenameSymbolAsync(symbolName, newName, filePath, previewOnly, cancellationToken);

        // Combine results
        var combinedResult = new RefactoringResult();

        if (csharpResult.Success && typeScriptResult.Success)
        {
            combinedResult.Success = true;
            combinedResult.Message = $"Successfully renamed symbol '{symbolName}' to '{newName}' in {csharpResult.FilesAffected} C# files and {typeScriptResult.FilesAffected} TypeScript files";
            combinedResult.Changes = csharpResult.Changes.Concat(typeScriptResult.Changes).ToList();
            combinedResult.FilesAffected = csharpResult.FilesAffected + typeScriptResult.FilesAffected;
        }
        else if (csharpResult.Success)
        {
            combinedResult = csharpResult;
            combinedResult.Message += $" (TypeScript: {typeScriptResult.Error})";
        }
        else if (typeScriptResult.Success)
        {
            combinedResult = typeScriptResult;
            combinedResult.Message += $" (C#: {csharpResult.Error})";
        }
        else
        {
            combinedResult.Success = false;
            combinedResult.Error = $"C#: {csharpResult.Error}; TypeScript: {typeScriptResult.Error}";
        }

        // Combine metadata
        foreach (KeyValuePair<string, object> kvp in csharpResult.Metadata)
        {
            combinedResult.Metadata[$"csharp_{kvp.Key}"] = kvp.Value;
        }
        foreach (KeyValuePair<string, object> kvp in typeScriptResult.Metadata)
        {
            combinedResult.Metadata[$"typescript_{kvp.Key}"] = kvp.Value;
        }

        combinedResult.Metadata["originalName"] = symbolName;
        combinedResult.Metadata["newName"] = newName;
        combinedResult.Metadata["renameMethod"] = "Mixed-CSharp+TypeScript";

        return combinedResult;
    }

    /// <summary>
    /// Original C# symbol renaming logic - ENHANCED VERSION
    /// </summary>
    private async Task<RefactoringResult> RenameSymbolCSharpAsync(
        string symbolName,
        string newName,
        string? filePath = null,
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = new RefactoringResult();
            string workspaceRoot = config.DefaultWorkspace;

            // ENHANCED: Resolve file path if provided
            string? resolvedFilePath = null;
            if (!string.IsNullOrEmpty(filePath))
            {
                resolvedFilePath = ValidateAndResolvePath(filePath);
                
                // Skip if the specified file is not C#
                if (!IsCSharpFile(resolvedFilePath))
                {
                    return new RefactoringResult
                    {
                        Success = false,
                        Error = $"File '{filePath}' is not a C# file"
                    };
                }
            }

            // Create backup if not preview
            string? backupId = null;
            if (!previewOnly)
            {
                backupId = await backupService.CreateBackupAsync(workspaceRoot, $"csharp_rename_{symbolName}_to_{newName}");
            }

            // ENHANCED: Try both MSBuild approach and simple file-based approach
            RenameResult? renameResult = await TryMsBuildRenameAsync(symbolName, newName, resolvedFilePath, cancellationToken) ??
                                         await TrySimpleRenameAsync(symbolName, newName, resolvedFilePath, cancellationToken);

            if (renameResult == null)
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"C# symbol '{symbolName}' not found in the project. Searched in {(resolvedFilePath != null ? $"file '{filePath}'" : "all C# files")}."
                };
            }

            // Apply changes if not preview
            if (!previewOnly && renameResult.Changes.Count != 0)
            {
                await ApplyChangesAsync(renameResult.Changes, symbolName, newName, backupId, cancellationToken);
            }

            result.Success = true;
            result.Message = previewOnly
                ? $"Preview: Renaming C# symbol '{symbolName}' to '{newName}' would affect {renameResult.Changes.Count} files"
                : $"Successfully renamed C# symbol '{symbolName}' to '{newName}' in {renameResult.Changes.Count} files";
            result.Changes = renameResult.Changes;
            result.FilesAffected = renameResult.Changes.Count;
            result.Metadata["originalName"] = symbolName;
            result.Metadata["newName"] = newName;
            result.Metadata["backupId"] = backupId ?? "";
            result.Metadata["renameMethod"] = renameResult.Method;

            return result;
        }
        catch (Exception ex)
        {
            return new RefactoringResult
            {
                Success = false,
                Error = $"C# symbol rename failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Determine which languages are present in the scope
    /// </summary>
    private LanguageScopeInfo DetermineLanguageScope(string? filePath)
    {
        var info = new LanguageScopeInfo();

        if (!string.IsNullOrEmpty(filePath))
        {
            // Specific file - check its extension
            string resolvedPath = ValidateAndResolvePath(filePath);
            info.HasCSharp = IsCSharpFile(resolvedPath);
            info.HasTypeScript = IsTypeScriptFile(resolvedPath);
        }
        else
        {
            // Workspace scope - check if there are files of each type
            string workspaceRoot = config.DefaultWorkspace;
            
            info.HasCSharp = Directory.GetFiles(workspaceRoot, "*.cs", SearchOption.AllDirectories)
                .Any(f => !IsExcludedDirectory(Path.GetDirectoryName(f) ?? ""));
                
            info.HasTypeScript = Directory.GetFiles(workspaceRoot, "*.ts", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(workspaceRoot, "*.tsx", SearchOption.AllDirectories))
                .Any(f => !IsExcludedDirectory(Path.GetDirectoryName(f) ?? ""));
        }

        return info;
    }

    /// <summary>
    /// Check if file is a C# file
    /// </summary>
    private static bool IsCSharpFile(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() == ".cs";
    }

    /// <summary>
    /// Check if file is a TypeScript file
    /// </summary>
    private static bool IsTypeScriptFile(string filePath)
    {
        string extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension is ".ts" or ".tsx";
    }

    /// <summary>
    /// ENHANCED: Try MSBuild-based rename for proper semantic analysis (C# only)
    /// </summary>
    private async Task<RenameResult?> TryMsBuildRenameAsync(
        string symbolName,
        string newName,
        string? filePath,
        CancellationToken cancellationToken)
    {
        try
        {
            string workspaceRoot = config.DefaultWorkspace;

            // Find solution or project files
            string[] projectFiles = Directory.GetFiles(workspaceRoot, "*.csproj", SearchOption.AllDirectories);
            string[] solutionFiles = Directory.GetFiles(workspaceRoot, "*.sln", SearchOption.AllDirectories);

            if (!projectFiles.Any() && !solutionFiles.Any())
            {
                return null; // Fall back to simple rename
            }

            using var workspace = MSBuildWorkspace.Create();

            // Load project or solution
            Project? project = null;
            Solution? solution = null;

            if (solutionFiles.Any())
            {
                solution = await workspace.OpenSolutionAsync(solutionFiles.First(), cancellationToken: cancellationToken);
                project = solution.Projects.FirstOrDefault();
            }
            else if (projectFiles.Any())
            {
                project = await workspace.OpenProjectAsync(projectFiles.First(), cancellationToken: cancellationToken);
                solution = project.Solution;
            }

            if (project == null || solution == null)
            {
                return null; // Fall back to simple rename
            }

            // ENHANCED: Find the symbol using improved search
            ISymbol? symbolToRename = await FindSymbolAsync(solution, symbolName, filePath, cancellationToken);

            if (symbolToRename == null)
            {
                return null; // Fall back to simple rename
            }

            // Perform the rename
            Solution newSolution = await Renamer.RenameSymbolAsync(
                solution,
                symbolToRename,
                new SymbolRenameOptions(),
                newName,
                cancellationToken);

            // Calculate changes
            List<FileChange> changes = await CalculateChangesAsync(solution, newSolution, cancellationToken);

            return new RenameResult
            {
                Changes = changes,
                Method = "MSBuild-Semantic"
            };
        }
        catch
        {
            return null; // Fall back to simple rename
        }
    }

    /// <summary>
    /// ENHANCED: Simple text-based rename as fallback (C# only)
    /// </summary>
    private async Task<RenameResult?> TrySimpleRenameAsync(
        string symbolName,
        string newName,
        string? filePath,
        CancellationToken cancellationToken)
    {
        try
        {
            string workspaceRoot = config.DefaultWorkspace;
            var changes = new List<FileChange>();

            // Get C# files to search
            IEnumerable<string> filesToSearch;
            if (!string.IsNullOrEmpty(filePath))
            {
                filesToSearch = [filePath];
            }
            else
            {
                filesToSearch = Directory.GetFiles(workspaceRoot, "*.cs", SearchOption.AllDirectories)
                    .Where(f => config.AllowedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .Where(f => !IsExcludedDirectory(Path.GetDirectoryName(f) ?? ""));
            }

            foreach (string file in filesToSearch)
            {
                if (!File.Exists(file)) continue;

                string content = await File.ReadAllTextAsync(file, cancellationToken);
                SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(content, cancellationToken: cancellationToken);
                SyntaxNode root = await syntaxTree.GetRootAsync(cancellationToken);

                // ENHANCED: Look for symbol declarations AND references
                List<SyntaxNode> symbolNodes = FindSymbolNodesInFile(root, symbolName);
                
                if (symbolNodes.Count == 0) continue;

                // Perform text-based replacement
                string modifiedContent = content;
                var wasModified = false;

                // Replace symbol declarations and references
                foreach (SyntaxNode symbolNode in symbolNodes.OrderByDescending(n => n.Span.Start))
                {
                    // Replace from end to start to maintain positions
                    TextSpan span = symbolNode.Span;
                    string before = modifiedContent[..span.Start];
                    string after = modifiedContent[span.End..];
                    modifiedContent = before + newName + after;
                    wasModified = true;
                }

                if (wasModified)
                {
                    var change = new FileChange
                    {
                        FilePath = file,
                        OriginalContent = content,
                        ModifiedContent = modifiedContent,
                        ChangeType = "SymbolRename"
                    };

                    // Calculate line changes
                    CalculateLineChanges(content, modifiedContent, change);
                    changes.Add(change);
                }
            }

            if (changes.Count != 0)
            {
                return new RenameResult
                {
                    Changes = changes,
                    Method = "Simple-TextBased"
                };
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// ENHANCED: Find symbol nodes (declarations and references) in a syntax tree
    /// </summary>
    private static List<SyntaxNode> FindSymbolNodesInFile(SyntaxNode root, string symbolName)
    {
        var symbolNodes = new List<SyntaxNode>();

        // Find method declarations (return the identifier token as SyntaxNode via cast)
        IEnumerable<SyntaxNode> methodDeclarations = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(m => m.Identifier.ValueText == symbolName)
            .Select(m => (SyntaxNode)m);

        // Find class declarations
        IEnumerable<SyntaxNode> classDeclarations = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Where(c => c.Identifier.ValueText == symbolName)
            .Select(c => (SyntaxNode)c);

        // Find property declarations
        IEnumerable<SyntaxNode> propertyDeclarations = root.DescendantNodes()
            .OfType<PropertyDeclarationSyntax>()
            .Where(p => p.Identifier.ValueText == symbolName)
            .Select(p => (SyntaxNode)p);

        // Find field declarations
        IEnumerable<SyntaxNode> fieldDeclarations = root.DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .SelectMany(f => f.Declaration.Variables)
            .Where(v => v.Identifier.ValueText == symbolName)
            .Select(v => (SyntaxNode)v);

        // Find variable declarations
        IEnumerable<SyntaxNode> variableDeclarations = root.DescendantNodes()
            .OfType<VariableDeclaratorSyntax>()
            .Where(v => v.Identifier.ValueText == symbolName)
            .Select(v => (SyntaxNode)v);

        // Find parameter declarations
        IEnumerable<SyntaxNode> parameterDeclarations = root.DescendantNodes()
            .OfType<ParameterSyntax>()
            .Where(p => p.Identifier.ValueText == symbolName)
            .Select(p => (SyntaxNode)p);

        // Find identifier references
        IEnumerable<SyntaxNode> identifierReferences = root.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(i => i.Identifier.ValueText == symbolName)
            .Cast<SyntaxNode>();

        // Combine all symbol nodes
        symbolNodes.AddRange(methodDeclarations);
        symbolNodes.AddRange(classDeclarations);
        symbolNodes.AddRange(propertyDeclarations);
        symbolNodes.AddRange(fieldDeclarations);
        symbolNodes.AddRange(variableDeclarations);
        symbolNodes.AddRange(parameterDeclarations);
        symbolNodes.AddRange(identifierReferences);

        return symbolNodes.Distinct().ToList();
    }

    /// <summary>
    /// ENHANCED: Improved symbol finding with better declaration detection
    /// </summary>
    private static async Task<ISymbol?> FindSymbolAsync(
        Solution solution,
        string symbolName,
        string? filePath,
        CancellationToken cancellationToken)
    {
        Document? targetDocument = null;

        if (!string.IsNullOrEmpty(filePath))
        {
            targetDocument = solution.Projects
                .SelectMany(p => p.Documents)
                .FirstOrDefault(d => d.FilePath != null &&
                    Path.GetFullPath(d.FilePath).Equals(Path.GetFullPath(filePath), StringComparison.OrdinalIgnoreCase));
        }

        // Search for the symbol
        foreach (Project proj in solution.Projects)
        {
            Compilation? compilation = await proj.GetCompilationAsync(cancellationToken);
            if (compilation == null) continue;

            foreach (Document doc in proj.Documents)
            {
                if (targetDocument != null && doc.Id != targetDocument.Id) continue;

                SyntaxTree? syntaxTree = await doc.GetSyntaxTreeAsync(cancellationToken);
                if (syntaxTree == null) continue;

                SemanticModel? semanticModel = compilation.GetSemanticModel(syntaxTree);
                SyntaxNode? root = await doc.GetSyntaxRootAsync(cancellationToken);

                if (root == null) continue;

                // ENHANCED: Look for symbol declarations first
                var symbolDeclarations = new List<SyntaxNode>();

                // Method declarations
                symbolDeclarations.AddRange(root.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .Where(m => m.Identifier.ValueText == symbolName));

                // Class declarations  
                symbolDeclarations.AddRange(root.DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .Where(c => c.Identifier.ValueText == symbolName));

                // Property declarations
                symbolDeclarations.AddRange(root.DescendantNodes()
                    .OfType<PropertyDeclarationSyntax>()
                    .Where(p => p.Identifier.ValueText == symbolName));

                // Field declarations
                symbolDeclarations.AddRange(root.DescendantNodes()
                    .OfType<FieldDeclarationSyntax>()
                    .SelectMany(f => f.Declaration.Variables)
                    .Where(v => v.Identifier.ValueText == symbolName));

                // Get symbol from declaration
                foreach (SyntaxNode declaration in symbolDeclarations)
                {
                    ISymbol? symbol = semanticModel?.GetDeclaredSymbol(declaration, cancellationToken);
                    if (symbol != null)
                    {
                        return symbol;
                    }
                }

                // Fall back to identifier references
                ISymbol? refSymbol = root
                    .DescendantNodes()
                    .OfType<IdentifierNameSyntax>()
                    .Where(id => id.Identifier.ValueText == symbolName)
                    .Select(id => semanticModel?.GetSymbolInfo(id, cancellationToken).Symbol)
                    .FirstOrDefault(s => s != null);

                if (refSymbol != null)
                {
                    return refSymbol;
                }
            }
        }

        return null;
    }

    private static async Task<List<FileChange>> CalculateChangesAsync(
        Solution originalSolution,
        Solution newSolution,
        CancellationToken cancellationToken)
    {
        var changes = new List<FileChange>();

        foreach (ProjectId projectId in originalSolution.ProjectIds)
        {
            Project? originalProject = originalSolution.GetProject(projectId);
            Project? newProject = newSolution.GetProject(projectId);

            if (originalProject == null || newProject == null) continue;

            foreach (DocumentId documentId in originalProject.DocumentIds)
            {
                Document? originalDocument = originalProject.GetDocument(documentId);
                Document? newDocument = newProject.GetDocument(documentId);

                if (originalDocument?.FilePath == null || newDocument == null) continue;

                SourceText originalText = await originalDocument.GetTextAsync(cancellationToken);
                SourceText newText = await newDocument.GetTextAsync(cancellationToken);

                if (!originalText.ContentEquals(newText))
                {
                    var change = new FileChange
                    {
                        FilePath = originalDocument.FilePath,
                        OriginalContent = originalText.ToString(),
                        ModifiedContent = newText.ToString(),
                        ChangeType = "SymbolRename"
                    };

                    // Calculate line changes
                    CalculateLineChanges(originalText.ToString(), newText.ToString(), change);
                    changes.Add(change);
                }
            }
        }

        return changes;
    }

    private static void CalculateLineChanges(string originalContent, string modifiedContent, FileChange change)
    {
        string[] originalLines = originalContent.Split('\n');
        string[] modifiedLines = modifiedContent.Split('\n');

        for (var i = 0; i < Math.Max(originalLines.Length, modifiedLines.Length); i++)
        {
            string originalLine = i < originalLines.Length ? originalLines[i] : "";
            string modifiedLine = i < modifiedLines.Length ? modifiedLines[i] : "";

            if (originalLine != modifiedLine)
            {
                change.LineChanges.Add(new LineChange
                {
                    LineNumber = i + 1,
                    Original = originalLine,
                    Modified = modifiedLine,
                    ChangeType = "Modified"
                });
            }
        }
    }

    private async Task ApplyChangesAsync(
        List<FileChange> changes,
        string symbolName,
        string newName,
        string? backupId,
        CancellationToken cancellationToken)
    {
        foreach (FileChange change in changes)
        {
            await File.WriteAllTextAsync(change.FilePath, change.ModifiedContent, cancellationToken);

            // Track the change
            await changeTracking.TrackChangeAsync(
                change.FilePath,
                change.OriginalContent,
                change.ModifiedContent,
                $"Rename symbol '{symbolName}' to '{newName}'",
                backupId);
        }
    }

    /// <summary>
    /// ENHANCED: Added path resolution similar to other services
    /// </summary>
    private string ValidateAndResolvePath(string path)
    {
        // Convert to absolute path
        string fullPath = Path.IsPathRooted(path) ? path : Path.Combine(config.DefaultWorkspace, path);
        fullPath = Path.GetFullPath(fullPath);

        // Security check: ensure path is within workspace if restricted
        if (config.Security.RestrictToWorkspace)
        {
            string workspaceFullPath = Path.GetFullPath(config.DefaultWorkspace);
            if (!fullPath.StartsWith(workspaceFullPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException($"Access denied: Path outside workspace: {path}");
            }
        }

        return fullPath;
    }

    private bool IsExcludedDirectory(string path)
    {
        string dirName = Path.GetFileName(path);
        return config.ExcludedDirectories.Contains(dirName);
    }

    /// <summary>
    /// Helper class for rename results
    /// </summary>
    private class RenameResult
    {
        public List<FileChange> Changes { get; set; } = [];
        public string Method { get; set; } = "";
    }

    /// <summary>
    /// Helper class for language scope information
    /// </summary>
    private class LanguageScopeInfo
    {
        public bool HasCSharp { get; set; }
        public bool HasTypeScript { get; set; }
    }
}
