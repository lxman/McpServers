using System.Text.RegularExpressions;
using McpCodeEditor.Interfaces;
using McpCodeEditor.Models;
using McpCodeEditor.Models.Refactoring;
using McpCodeEditor.Services.Analysis;

namespace McpCodeEditor.Services.Refactoring.TypeScript;

/// <summary>
/// Advanced TypeScript cross-file symbol renaming service with dependency analysis
/// </summary>
public class TypeScriptCrossFileRenamer(
    TypeScriptProjectAnalyzer projectAnalyzer,
    TypeScriptAnalysisService analysisService,
    CodeEditorConfigurationService config,
    IBackupService backupService,
    IChangeTrackingService changeTracking)
{
    /// <summary>
    /// Represents a planned rename operation for a file
    /// </summary>
    private class FileRenameOperation
    {
        public string FilePath { get; set; } = string.Empty;
        public string OriginalContent { get; set; } = string.Empty;
        public string ModifiedContent { get; set; } = string.Empty;
        public List<SymbolRename> SymbolRenames { get; set; } = [];
        public List<ImportExportChange> ImportExportChanges { get; set; } = [];
    }

    /// <summary>
    /// Represents a single symbol rename within a file
    /// </summary>
    private class SymbolRename
    {
        public string OriginalName { get; set; } = string.Empty;
        public string NewName { get; set; } = string.Empty;
        public int LineNumber { get; set; }
        public int Column { get; set; }
        public string RenameType { get; set; } = string.Empty; // definition, usage, import, export
    }

    /// <summary>
    /// Represents changes to import/export statements
    /// </summary>
    private class ImportExportChange
    {
        public int LineNumber { get; set; }
        public string OriginalStatement { get; set; } = string.Empty;
        public string ModifiedStatement { get; set; } = string.Empty;
        public string ChangeType { get; set; } = string.Empty; // import, export
    }

    /// <summary>
    /// Perform intelligent cross-file symbol renaming with unified response format
    /// </summary>
    public async Task<SummaryRefactoringResult> RenameSymbolAcrossFilesUnifiedAsync(
        string symbolName,
        string newName,
        string? rootPath = null,
        bool previewOnly = false,
        bool useSummaryMode = true,
        CancellationToken cancellationToken = default)
    {
        RefactoringResult fullResult = await RenameSymbolAcrossFilesAsync(
            symbolName, newName, rootPath, previewOnly, cancellationToken);

        if (!fullResult.Success)
        {
            return new SummaryRefactoringResult
            {
                Success = false,
                Error = fullResult.Error,
                Message = fullResult.Message
            };
        }

        if (useSummaryMode)
        {
            SummaryRefactoringResult summary = ConvertToSummaryResult(fullResult);
            summary.Warning = "??  This operation used summary mode to prevent large responses. " +
                              "To get full file content details, set 'useSummaryMode=false' but be aware " +
                              "this may return 100k+ characters and could terminate the conversation.";
            return summary;
        }
        else
        {
            SummaryRefactoringResult fullSummary = ConvertToFullSummaryResult(fullResult);
            fullSummary.Warning = "?? LARGE RESPONSE: This result contains full file content and may be very large. " +
                                  "Consider using useSummaryMode=true for future operations to prevent conversation termination.";
            return fullSummary;
        }
    }

    /// <summary>
    /// Perform intelligent cross-file symbol renaming with summary results to prevent large responses
    /// </summary>
    public async Task<SummaryRefactoringResult> RenameSymbolAcrossFilesSummaryAsync(
        string symbolName,
        string newName,
        string? rootPath = null,
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        RefactoringResult fullResult = await RenameSymbolAcrossFilesAsync(
            symbolName, newName, rootPath, previewOnly, cancellationToken);

        return ConvertToSummaryResult(fullResult);
    }

    /// <summary>
    /// Perform intelligent cross-file symbol renaming with full detailed results (WARNING: May be very large)
    /// </summary>
    public async Task<RefactoringResult> RenameSymbolAcrossFilesAsync(
        string symbolName,
        string newName,
        string? rootPath = null,
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = new RefactoringResult();
            string projectRoot = rootPath ?? config.DefaultWorkspace;

            // Validate symbol names
            ValidateSymbolNames(symbolName, newName);

            // Analyze project to understand symbol dependencies
            TypeScriptProjectAnalyzer.ProjectAnalysisResult projectAnalysis = await projectAnalyzer.AnalyzeProjectAsync(projectRoot, cancellationToken);
            if (!projectAnalysis.Success)
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"Project analysis failed: {projectAnalysis.Error}"
                };
            }

            // Find the symbol and its references
            if (!projectAnalysis.Symbols.TryGetValue(symbolName, out TypeScriptProjectAnalyzer.ProjectSymbolInfo? symbolInfo))
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"Symbol '{symbolName}' not found in project"
                };
            }

            // Create backup if not preview
            string? backupId = null;
            if (!previewOnly)
            {
                backupId = await backupService.CreateBackupAsync(
                    projectRoot, 
                    $"cross_file_rename_{symbolName}_to_{newName}");
            }

            // Plan rename operations for all affected files
            List<FileRenameOperation> renameOperations = await PlanRenameOperationsAsync(
                symbolInfo, 
                projectAnalysis, 
                symbolName, 
                newName, 
                cancellationToken);

            if (renameOperations.Count == 0)
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"No renameable occurrences found for symbol '{symbolName}'"
                };
            }

            // Execute rename operations
            var changes = new List<FileChange>();
            foreach (FileRenameOperation operation in renameOperations)
            {
                FileChange change = await ExecuteRenameOperationAsync(operation, cancellationToken);
                changes.Add(change);
            }

            // Apply changes if not preview
            if (!previewOnly)
            {
                await ApplyChangesAsync(changes, symbolName, newName, backupId, cancellationToken);
            }

            // Build result
            result.Success = true;
            result.Message = previewOnly
                ? $"Preview: Cross-file rename of '{symbolName}' to '{newName}' would affect {changes.Count} files"
                : $"Successfully renamed '{symbolName}' to '{newName}' across {changes.Count} files";
            result.Changes = changes;
            result.FilesAffected = changes.Count;
            result.Metadata["originalName"] = symbolName;
            result.Metadata["newName"] = newName;
            result.Metadata["backupId"] = backupId ?? "";
            result.Metadata["renameMethod"] = "TypeScript-CrossFile";
            result.Metadata["symbolType"] = symbolInfo.SymbolType;
            result.Metadata["definitionFile"] = symbolInfo.DefinitionFile;

            return result;
        }
        catch (Exception ex)
        {
            return new RefactoringResult
            {
                Success = false,
                Error = $"Cross-file rename failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Convert full RefactoringResult to SummaryRefactoringResult to prevent large responses
    /// </summary>
    private static SummaryRefactoringResult ConvertToSummaryResult(RefactoringResult fullResult)
    {
        if (!fullResult.Success)
        {
            return new SummaryRefactoringResult
            {
                Success = false,
                Error = fullResult.Error,
                Message = fullResult.Message
            };
        }

        var summaryChanges = new List<SummaryFileChange>();
        var totalLinesChanged = 0;

        foreach (FileChange change in fullResult.Changes)
        {
            var summaryChange = new SummaryFileChange
            {
                FilePath = change.FilePath,
                FileName = Path.GetFileName(change.FilePath),
                ChangeType = change.ChangeType,
                TotalChanges = change.LineChanges.Count,
                ModifiedLineNumbers = change.LineChanges.Select(lc => lc.LineNumber).ToList()
            };

            // Add sample changes (first 3)
            List<SummaryLineChange> sampleChanges = change.LineChanges.Take(3).Select(lc => new SummaryLineChange
            {
                LineNumber = lc.LineNumber,
                ChangeType = lc.ChangeType,
                ChangeDescription = $"Line {lc.LineNumber}: Symbol renamed",
                SampleBefore = TruncateLineForSample(lc.Original),
                SampleAfter = TruncateLineForSample(lc.Modified)
            }).ToList();

            summaryChange.SampleChanges = sampleChanges;

            // Generate change summary
            summaryChange.ChangeSummary = GenerateChangeSummary(change);

            summaryChanges.Add(summaryChange);
            totalLinesChanged += change.LineChanges.Count;
        }

        return new SummaryRefactoringResult
        {
            Success = true,
            Message = fullResult.Message,
            Changes = summaryChanges,
            FilesAffected = fullResult.FilesAffected,
            TotalLinesChanged = totalLinesChanged,
            Metadata = fullResult.Metadata,
            BackupId = fullResult.Metadata.TryGetValue("backupId", out object? backupIdObj) 
                ? backupIdObj?.ToString() 
                : null
        };
    }

    /// <summary>
    /// Convert RefactoringResult to SummaryRefactoringResult with full content included
    /// </summary>
    private static SummaryRefactoringResult ConvertToFullSummaryResult(RefactoringResult fullResult)
    {
        var summaryChanges = new List<SummaryFileChange>();
        var totalLinesChanged = 0;

        foreach (FileChange change in fullResult.Changes)
        {
            var summaryChange = new SummaryFileChange
            {
                FilePath = change.FilePath,
                FileName = Path.GetFileName(change.FilePath),
                ChangeType = change.ChangeType,
                TotalChanges = change.LineChanges.Count,
                ModifiedLineNumbers = change.LineChanges.Select(lc => lc.LineNumber).ToList(),
                OriginalContent = change.OriginalContent,
                ModifiedContent = change.ModifiedContent
            };

            // Add all changes with full content
            List<SummaryLineChange> fullChanges = change.LineChanges.Select(lc => new SummaryLineChange
            {
                LineNumber = lc.LineNumber,
                ChangeType = lc.ChangeType,
                ChangeDescription = $"Line {lc.LineNumber}: Symbol renamed",
                SampleBefore = TruncateLineForSample(lc.Original),
                SampleAfter = TruncateLineForSample(lc.Modified),
                FullBefore = lc.Original,
                FullAfter = lc.Modified
            }).ToList();

            summaryChange.SampleChanges = fullChanges;
            summaryChange.ChangeSummary = GenerateChangeSummary(change);

            summaryChanges.Add(summaryChange);
            totalLinesChanged += change.LineChanges.Count;
        }

        return new SummaryRefactoringResult
        {
            Success = true,
            Message = fullResult.Message,
            Changes = summaryChanges,
            FilesAffected = fullResult.FilesAffected,
            TotalLinesChanged = totalLinesChanged,
            Metadata = fullResult.Metadata,
            BackupId = fullResult.Metadata.TryGetValue("backupId", out object? backupIdObj) 
                ? backupIdObj?.ToString() 
                : null,
            ContainsFullContent = true
        };
    }

    /// <summary>
    /// Truncate line content for sample display
    /// </summary>
    private static string TruncateLineForSample(string line)
    {
        const int maxLength = 60;
        if (string.IsNullOrEmpty(line)) return "";
        
        string trimmed = line.Trim();
        if (trimmed.Length <= maxLength) return trimmed;
        
        return trimmed[..(maxLength - 3)] + "...";
    }

    /// <summary>
    /// Generate a human-readable summary of changes for a file
    /// </summary>
    private static string GenerateChangeSummary(FileChange change)
    {
        int totalChanges = change.LineChanges.Count;
        if (totalChanges == 0) return "No changes";
        
        if (totalChanges == 1)
        {
            return $"1 line modified at line {change.LineChanges[0].LineNumber}";
        }
        
        List<int> lineNumbers = change.LineChanges.Select(lc => lc.LineNumber).OrderBy(x => x).ToList();
        if (totalChanges <= 3)
        {
            return $"{totalChanges} lines modified at lines {string.Join(", ", lineNumbers)}";
        }
        
        return $"{totalChanges} lines modified (lines {lineNumbers.First()}-{lineNumbers.Last()})";
    }

    /// <summary>
    /// Plan rename operations for all files that need changes
    /// </summary>
    private static async Task<List<FileRenameOperation>> PlanRenameOperationsAsync(
        TypeScriptProjectAnalyzer.ProjectSymbolInfo symbolInfo,
        TypeScriptProjectAnalyzer.ProjectAnalysisResult projectAnalysis,
        string symbolName,
        string newName,
        CancellationToken cancellationToken)
    {
        var operations = new List<FileRenameOperation>();
        var affectedFiles = new HashSet<string>();

        // Add definition file
        affectedFiles.Add(symbolInfo.DefinitionFile);

        // Add files that import this symbol
        affectedFiles.UnionWith(symbolInfo.ImportedBy);

        // Add files with references
        foreach (TypeScriptProjectAnalyzer.CrossFileReference reference in symbolInfo.References)
        {
            affectedFiles.Add(reference.SourceFile);
        }

        // Add files with cross-file references
        IEnumerable<TypeScriptProjectAnalyzer.CrossFileReference> crossFileRefs = projectAnalysis.CrossFileReferences
            .Where(r => r.SymbolName == symbolName);
        foreach (TypeScriptProjectAnalyzer.CrossFileReference reference in crossFileRefs)
        {
            affectedFiles.Add(reference.SourceFile);
            affectedFiles.Add(reference.TargetFile);
        }

        // Create operations for each affected file
        foreach (string filePath in affectedFiles)
        {
            if (!File.Exists(filePath))
                continue;

            FileRenameOperation operation = await PlanFileRenameOperationAsync(
                filePath, 
                symbolName, 
                newName, 
                symbolInfo, 
                projectAnalysis, 
                cancellationToken);

            if (operation.SymbolRenames.Count > 0 || operation.ImportExportChanges.Count > 0)
            {
                operations.Add(operation);
            }
        }

        return operations;
    }

    /// <summary>
    /// Plan rename operation for a single file
    /// </summary>
    private static async Task<FileRenameOperation> PlanFileRenameOperationAsync(
        string filePath,
        string symbolName,
        string newName,
        TypeScriptProjectAnalyzer.ProjectSymbolInfo symbolInfo,
        TypeScriptProjectAnalyzer.ProjectAnalysisResult projectAnalysis,
        CancellationToken cancellationToken)
    {
        var operation = new FileRenameOperation
        {
            FilePath = filePath,
            OriginalContent = await File.ReadAllTextAsync(filePath, cancellationToken)
        };

        // Plan symbol definition rename if this is the definition file
        if (filePath == symbolInfo.DefinitionFile)
        {
            operation.SymbolRenames.Add(new SymbolRename
            {
                OriginalName = symbolName,
                NewName = newName,
                LineNumber = symbolInfo.DefinitionLine,
                Column = 0, // Will be calculated more precisely later
                RenameType = "definition"
            });
        }

        // Plan import/export statement changes
        await PlanImportExportChangesAsync(operation, symbolName, newName, projectAnalysis);

        // Plan usage renames within the file
        await PlanUsageRenamesAsync(operation, symbolName, newName);

        return operation;
    }

    /// <summary>
    /// Plan changes to import and export statements
    /// </summary>
    private static async Task PlanImportExportChangesAsync(
        FileRenameOperation operation,
        string symbolName,
        string newName,
        TypeScriptProjectAnalyzer.ProjectAnalysisResult projectAnalysis)
    {
        await Task.CompletedTask; // Make async for consistency

        string content = operation.OriginalContent;
        string[] lines = content.Split('\n');

        // Find import statements that reference the symbol
        var importPattern = $@"import\s*\{{[^}}]*\b{Regex.Escape(symbolName)}\b[^}}]*\}}\s*from";
        MatchCollection importMatches = Regex.Matches(content, importPattern, RegexOptions.Multiline);

        foreach (Match match in importMatches)
        {
            int lineNumber = GetLineNumber(content, match.Index);
            string originalLine = lines[lineNumber - 1];
            string modifiedLine = originalLine.Replace(symbolName, newName);

            operation.ImportExportChanges.Add(new ImportExportChange
            {
                LineNumber = lineNumber,
                OriginalStatement = originalLine.Trim(),
                ModifiedStatement = modifiedLine.Trim(),
                ChangeType = "import"
            });
        }

        // Find export statements that reference the symbol
        var exportPattern = $@"export\s*\{{[^}}]*\b{Regex.Escape(symbolName)}\b[^}}]*\}}";
        MatchCollection exportMatches = Regex.Matches(content, exportPattern, RegexOptions.Multiline);

        foreach (Match match in exportMatches)
        {
            int lineNumber = GetLineNumber(content, match.Index);
            string originalLine = lines[lineNumber - 1];
            string modifiedLine = originalLine.Replace(symbolName, newName);

            operation.ImportExportChanges.Add(new ImportExportChange
            {
                LineNumber = lineNumber,
                OriginalStatement = originalLine.Trim(),
                ModifiedStatement = modifiedLine.Trim(),
                ChangeType = "export"
            });
        }
    }

    /// <summary>
    /// Plan symbol usage renames within a file
    /// </summary>
    private static async Task PlanUsageRenamesAsync(
        FileRenameOperation operation,
        string symbolName,
        string newName)
    {
        await Task.CompletedTask; // Make async for consistency

        string content = operation.OriginalContent;

        // Find various usage patterns
        var usagePatterns = new[]
        {
            ($@"\b{Regex.Escape(symbolName)}\s*\(", "function_call"),
            ($@"new\s+{Regex.Escape(symbolName)}\b", "constructor"),
            ($@":\s*{Regex.Escape(symbolName)}\b", "type_annotation"),
            ($@"\b{Regex.Escape(symbolName)}\.[^\w]+", "property_access"),
            ($@"\b{Regex.Escape(symbolName)}\b", "general_reference")
        };

        foreach ((string? pattern, string usageType) in usagePatterns)
        {
            MatchCollection matches = Regex.Matches(content, pattern, RegexOptions.Multiline);
            foreach (Match match in matches)
            {
                // Skip if this is already covered by import/export changes
                int lineNumber = GetLineNumber(content, match.Index);
                bool isImportExport = operation.ImportExportChanges
                    .Any(ie => ie.LineNumber == lineNumber);

                if (!isImportExport)
                {
                    operation.SymbolRenames.Add(new SymbolRename
                    {
                        OriginalName = symbolName,
                        NewName = newName,
                        LineNumber = lineNumber,
                        Column = GetColumnNumber(content, match.Index),
                        RenameType = usageType
                    });
                }
            }
        }
    }

    /// <summary>
    /// Execute a single file rename operation
    /// </summary>
    private static async Task<FileChange> ExecuteRenameOperationAsync(
        FileRenameOperation operation,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // Make async for consistency

        string modifiedContent = operation.OriginalContent;

        // Apply import/export changes first (line-based)
        if (operation.ImportExportChanges.Count > 0)
        {
            string[] lines = modifiedContent.Split('\n');
            foreach (ImportExportChange change in operation.ImportExportChanges.OrderByDescending(c => c.LineNumber))
            {
                if (change.LineNumber <= lines.Length)
                {
                    lines[change.LineNumber - 1] = change.ModifiedStatement;
                }
            }
            modifiedContent = string.Join('\n', lines);
        }

        // Apply symbol renames (regex-based for more precise matching)
        foreach (SymbolRename rename in operation.SymbolRenames)
        {
            modifiedContent = ApplySymbolRename(modifiedContent, rename);
        }

        operation.ModifiedContent = modifiedContent;

        var fileChange = new FileChange
        {
            FilePath = operation.FilePath,
            OriginalContent = operation.OriginalContent,
            ModifiedContent = modifiedContent,
            ChangeType = "TypeScript-CrossFileRename"
        };

        CalculateLineChanges(operation.OriginalContent, modifiedContent, fileChange);
        return fileChange;
    }

    /// <summary>
    /// Apply a single symbol rename to content
    /// </summary>
    private static string ApplySymbolRename(string content, SymbolRename rename)
    {
        string pattern = rename.RenameType switch
        {
            "definition" => $@"\b{Regex.Escape(rename.OriginalName)}\b",
            "function_call" => $@"\b{Regex.Escape(rename.OriginalName)}(?=\s*\()",
            "constructor" => $@"(?<=new\s+){Regex.Escape(rename.OriginalName)}\b",
            "type_annotation" => $@"(?<=:\s*){Regex.Escape(rename.OriginalName)}\b",
            "property_access" => $@"\b{Regex.Escape(rename.OriginalName)}(?=\.)",
            _ => $@"\b{Regex.Escape(rename.OriginalName)}\b"
        };

        return Regex.Replace(content, pattern, rename.NewName, RegexOptions.Multiline);
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

            await changeTracking.TrackChangeAsync(
                change.FilePath,
                change.OriginalContent,
                change.ModifiedContent,
                $"Cross-file rename TypeScript symbol '{symbolName}' to '{newName}'",
                backupId);
        }
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
    /// Validate symbol names for TypeScript compliance
    /// </summary>
    private static void ValidateSymbolNames(string symbolName, string newName)
    {
        if (string.IsNullOrWhiteSpace(symbolName))
            throw new ArgumentException("Symbol name cannot be empty", nameof(symbolName));

        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("New name cannot be empty", nameof(newName));

        // Check if names are valid TypeScript identifiers
        var identifierPattern = @"^[a-zA-Z_$][a-zA-Z0-9_$]*$";
        
        if (!Regex.IsMatch(symbolName, identifierPattern))
            throw new ArgumentException($"'{symbolName}' is not a valid TypeScript identifier", nameof(symbolName));

        if (!Regex.IsMatch(newName, identifierPattern))
            throw new ArgumentException($"'{newName}' is not a valid TypeScript identifier", nameof(newName));

        // Check for TypeScript reserved words
        var reservedWords = new HashSet<string>
        {
            "abstract", "any", "as", "boolean", "break", "case", "catch", "class", "const", "continue",
            "debugger", "declare", "default", "delete", "do", "else", "enum", "export", "extends",
            "false", "finally", "for", "from", "function", "get", "if", "implements", "import",
            "in", "instanceof", "interface", "let", "module", "namespace", "never", "new", "null",
            "number", "object", "of", "package", "private", "protected", "public", "readonly",
            "return", "set", "static", "string", "super", "switch", "symbol", "this", "throw",
            "true", "try", "type", "typeof", "undefined", "unique", "unknown", "var", "void",
            "while", "with", "yield"
        };

        if (reservedWords.Contains(newName.ToLowerInvariant()))
            throw new ArgumentException($"'{newName}' is a reserved TypeScript keyword", nameof(newName));
    }

    /// <summary>
    /// Get line number from character index
    /// </summary>
    private static int GetLineNumber(string content, int index)
    {
        return content.Take(index).Count(c => c == '\n') + 1;
    }

    /// <summary>
    /// Get column number from character index
    /// </summary>
    private static int GetColumnNumber(string content, int index)
    {
        int lastNewLine = content.LastIndexOf('\n', index);
        return index - lastNewLine;
    }
}
