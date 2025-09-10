using System.Text.RegularExpressions;
using McpCodeEditor.Interfaces;
using McpCodeEditor.Models;
using McpCodeEditor.Models.Refactoring;
using McpCodeEditor.Services.Analysis;

namespace McpCodeEditor.Services.Refactoring;

/// <summary>
/// Service responsible for TypeScript symbol renaming operations using regex-based analysis
/// </summary>
public class TypeScriptSymbolRenameService(
    TypeScriptAnalysisService analysisService,
    CodeEditorConfigurationService config,
    IBackupService backupService,
    IChangeTrackingService changeTracking)
{
    /// <summary>
    /// Rename a TypeScript symbol across files
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
            var result = new RefactoringResult();
            string workspaceRoot = config.DefaultWorkspace;

            // Resolve file path if provided
            string? resolvedFilePath = null;
            if (!string.IsNullOrEmpty(filePath))
            {
                resolvedFilePath = ValidateAndResolvePath(filePath);
            }

            // Create backup if not preview
            string? backupId = null;
            if (!previewOnly)
            {
                backupId = await backupService.CreateBackupAsync(workspaceRoot, $"ts_rename_{symbolName}_to_{newName}");
            }

            // Get TypeScript files to process
            IEnumerable<string> filesToProcess = GetTypeScriptFiles(resolvedFilePath);

            var changes = new List<FileChange>();

            foreach (string file in filesToProcess)
            {
                if (!File.Exists(file)) continue;

                // Analyze the file to understand its structure
                TypeScriptAnalysisResult analysisResult = await analysisService.AnalyzeFileAsync(file, cancellationToken);
                if (!analysisResult.Success) continue;

                // Find symbols matching the target name
                List<TypeScriptSymbol> symbols = TypeScriptAnalysisService.FindSymbolsByName(analysisResult, symbolName);
                if (symbols.Count == 0) continue;

                // Read file content
                string content = await File.ReadAllTextAsync(file, cancellationToken);

                // Perform symbol renaming
                string modifiedContent = await RenameSymbolInContentAsync(content, symbolName, newName, analysisResult, cancellationToken);

                if (content != modifiedContent)
                {
                    var change = new FileChange
                    {
                        FilePath = file,
                        OriginalContent = content,
                        ModifiedContent = modifiedContent,
                        ChangeType = "TypeScriptSymbolRename"
                    };

                    CalculateLineChanges(content, modifiedContent, change);
                    changes.Add(change);
                }
            }

            if (changes.Count == 0)
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"TypeScript symbol '{symbolName}' not found in {(resolvedFilePath != null ? $"file '{filePath}'" : "any TypeScript files")}."
                };
            }

            // Apply changes if not preview
            if (!previewOnly)
            {
                await ApplyChangesAsync(changes, symbolName, newName, backupId, cancellationToken);
            }

            result.Success = true;
            result.Message = previewOnly
                ? $"Preview: Renaming TypeScript symbol '{symbolName}' to '{newName}' would affect {changes.Count} files"
                : $"Successfully renamed TypeScript symbol '{symbolName}' to '{newName}' in {changes.Count} files";
            result.Changes = changes;
            result.FilesAffected = changes.Count;
            result.Metadata["originalName"] = symbolName;
            result.Metadata["newName"] = newName;
            result.Metadata["backupId"] = backupId ?? "";
            result.Metadata["renameMethod"] = "TypeScript-RegexBased";

            return result;
        }
        catch (Exception ex)
        {
            return new RefactoringResult
            {
                Success = false,
                Error = $"TypeScript symbol rename failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Rename symbol occurrences in TypeScript content using intelligent regex patterns
    /// </summary>
    private static async Task<string> RenameSymbolInContentAsync(
        string content,
        string symbolName,
        string newName,
        TypeScriptAnalysisResult analysisResult,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // Make async for consistency
        
        // Build comprehensive regex patterns to match symbol usage
        var patterns = new List<string>();

        // 1. Function declarations: function symbolName(...)
        patterns.Add($@"\bfunction\s+{Regex.Escape(symbolName)}\b");

        // 2. Variable/const/let declarations: const symbolName =, let symbolName =
        patterns.Add($@"\b(?:const|let|var)\s+{Regex.Escape(symbolName)}\b");

        // 3. Class declarations: class symbolName
        patterns.Add($@"\bclass\s+{Regex.Escape(symbolName)}\b");

        // 4. Interface declarations: interface symbolName
        patterns.Add($@"\binterface\s+{Regex.Escape(symbolName)}\b");

        // 5. Type alias declarations: type symbolName =
        patterns.Add($@"\btype\s+{Regex.Escape(symbolName)}\b");

        // 6. Method declarations: symbolName(...) {
        patterns.Add($@"\b{Regex.Escape(symbolName)}\s*\(");

        // 7. Property access: obj.symbolName
        patterns.Add($@"\.{Regex.Escape(symbolName)}\b");

        // 8. Function calls: symbolName(...)
        patterns.Add($@"\b{Regex.Escape(symbolName)}\s*\(");

        // 9. Import/export statements: import { symbolName }, export { symbolName }
        patterns.Add($@"(?:import|export)\s*\{{[^}}]*\b{Regex.Escape(symbolName)}\b[^}}]*\}}");

        // 10. Object property shorthand: { symbolName } or { symbolName: ... }
        patterns.Add($@"\{{\s*{Regex.Escape(symbolName)}\b");

        // 11. Standalone identifier references
        patterns.Add($@"\b{Regex.Escape(symbolName)}\b");

        string modifiedContent = content;

        // Apply replacements in order of specificity (most specific first)
        foreach (string pattern in patterns)
        {
            try
            {
                modifiedContent = Regex.Replace(modifiedContent, pattern, match =>
                {
                    // Replace only the symbol name part, preserving surrounding syntax
                    return match.Value.Replace(symbolName, newName);
                }, RegexOptions.Multiline);
            }
            catch (Exception)
            {
                // Skip problematic patterns
                continue;
            }
        }

        return modifiedContent;
    }

    /// <summary>
    /// Get TypeScript files to process based on file path or workspace scan
    /// </summary>
    private IEnumerable<string> GetTypeScriptFiles(string? resolvedFilePath)
    {
        if (!string.IsNullOrEmpty(resolvedFilePath))
        {
            // Process specific file if it's TypeScript
            if (IsTypeScriptFile(resolvedFilePath))
            {
                return [resolvedFilePath];
            }
            return [];
        }

        // Scan workspace for TypeScript files
        string workspaceRoot = config.DefaultWorkspace;
        return Directory.GetFiles(workspaceRoot, "*.ts", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(workspaceRoot, "*.tsx", SearchOption.AllDirectories))
            .Where(f => !IsExcludedDirectory(Path.GetDirectoryName(f) ?? ""))
            .Where(IsTypeScriptFile);
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
    /// Calculate line-by-line changes for the file change
    /// </summary>
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

    /// <summary>
    /// Apply file changes to disk
    /// </summary>
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
                $"Rename TypeScript symbol '{symbolName}' to '{newName}'",
                backupId);
        }
    }

    /// <summary>
    /// Validate and resolve file path
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

    /// <summary>
    /// Check if directory should be excluded
    /// </summary>
    private bool IsExcludedDirectory(string path)
    {
        string dirName = Path.GetFileName(path);
        return config.ExcludedDirectories.Contains(dirName);
    }
}
