using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using DiffPlex.Model;
using DiffPlex.Chunkers;

namespace McpCodeEditor.Services;

public class DiffService
{
    private readonly CodeEditorConfigurationService _config;
    private readonly IDiffer _differ;
    private readonly ISideBySideDiffBuilder _sideBySideDiffBuilder;
    private readonly IInlineDiffBuilder _inlineDiffBuilder;

    public DiffService(CodeEditorConfigurationService config)
    {
        _config = config;
        _differ = new Differ();
        _sideBySideDiffBuilder = new SideBySideDiffBuilder(_differ);
        _inlineDiffBuilder = new InlineDiffBuilder(_differ);
    }

    public async Task<object> GenerateAsync(string? originalPath, string? modifiedPath,
        string? originalContent, string? modifiedContent, string format, int contextLines)
    {
        try
        {
            // Get original content
            string original;
            if (!string.IsNullOrEmpty(originalPath))
            {
                if (!File.Exists(originalPath))
                {
                    return new { success = false, error = $"Original file not found: {originalPath}" };
                }
                original = await File.ReadAllTextAsync(originalPath);
            }
            else if (!string.IsNullOrEmpty(originalContent))
            {
                original = originalContent;
            }
            else
            {
                return new { success = false, error = "Either original_path or original_content must be provided" };
            }

            // Get modified content
            string modified;
            if (!string.IsNullOrEmpty(modifiedPath))
            {
                if (!File.Exists(modifiedPath))
                {
                    return new { success = false, error = $"Modified file not found: {modifiedPath}" };
                }
                modified = await File.ReadAllTextAsync(modifiedPath);
            }
            else if (!string.IsNullOrEmpty(modifiedContent))
            {
                modified = modifiedContent;
            }
            else
            {
                return new { success = false, error = "Either modified_path or modified_content must be provided" };
            }

            object result = format.ToLowerInvariant() switch
            {
                "side-by-side" or "sidebyside" => GenerateSideBySideDiff(original, modified, originalPath, modifiedPath),
                "inline" => GenerateInlineDiff(original, modified, originalPath, modifiedPath),
                "unified" or _ => GenerateUnifiedDiff(original, modified, originalPath, modifiedPath, contextLines)
            };

            return result;
        }
        catch (Exception ex)
        {
            return new { success = false, error = ex.Message };
        }
    }

    private object GenerateUnifiedDiff(string original, string modified, string? originalPath, string? modifiedPath, int contextLines)
    {
        DiffResult? diff = _differ.CreateDiffs(original, modified, true, false, new LineChunker());
        string unifiedDiff = BuildUnifiedDiff(diff, originalPath ?? "original", modifiedPath ?? "modified", contextLines);

        object stats = CalculateStats(diff);

        return new
        {
            success = true,
            format = "unified",
            original_file = originalPath,
            modified_file = modifiedPath,
            context_lines = contextLines,
            statistics = stats,
            diff_content = unifiedDiff
        };
    }

    private object GenerateSideBySideDiff(string original, string modified, string? originalPath, string? modifiedPath)
    {
        SideBySideDiffModel? diffResult = _sideBySideDiffBuilder.BuildDiffModel(original, modified);

        var leftLines = diffResult.OldText.Lines.Select(line => new
        {
            line_number = line.Position,
            content = line.Text,
            type = GetChangeType(line.Type)
        }).ToList();

        var rightLines = diffResult.NewText.Lines.Select(line => new
        {
            line_number = line.Position,
            content = line.Text,
            type = GetChangeType(line.Type)
        }).ToList();

        var stats = new
        {
            lines_added = rightLines.Count(l => l.type == "added"),
            lines_deleted = leftLines.Count(l => l.type == "deleted"),
            lines_modified = rightLines.Count(l => l.type == "modified"),
            lines_unchanged = rightLines.Count(l => l.type == "unchanged")
        };

        return new
        {
            success = true,
            format = "side-by-side",
            original_file = originalPath,
            modified_file = modifiedPath,
            statistics = stats,
            left_side = new
            {
                title = originalPath ?? "Original",
                lines = leftLines
            },
            right_side = new
            {
                title = modifiedPath ?? "Modified",
                lines = rightLines
            }
        };
    }

    private object GenerateInlineDiff(string original, string modified, string? originalPath, string? modifiedPath)
    {
        DiffPaneModel? diffResult = _inlineDiffBuilder.BuildDiffModel(original, modified);

        var lines = diffResult.Lines.Select((line, index) => new
        {
            line_number = index + 1,
            content = line.Text,
            type = GetChangeType(line.Type),
            old_line_number = line.Type == ChangeType.Inserted ? (int?)null : line.Position,
            new_line_number = line.Type == ChangeType.Deleted ? (int?)null : line.Position
        }).ToList();

        var stats = new
        {
            lines_added = lines.Count(l => l.type == "added"),
            lines_deleted = lines.Count(l => l.type == "deleted"),
            lines_modified = lines.Count(l => l.type == "modified"),
            lines_unchanged = lines.Count(l => l.type == "unchanged")
        };

        return new
        {
            success = true,
            format = "inline",
            original_file = originalPath,
            modified_file = modifiedPath,
            statistics = stats,
            lines = lines
        };
    }

    private static string BuildUnifiedDiff(DiffResult diff, string originalFile, string modifiedFile, int contextLines)
    {
        var result = new System.Text.StringBuilder();

        // Add header
        result.AppendLine($"--- {originalFile}");
        result.AppendLine($"+++ {modifiedFile}");

        var pieceIndex = 0;
        var originalLineNumber = 1;
        var modifiedLineNumber = 1;

        while (pieceIndex < diff.DiffBlocks.Count)
        {
            DiffBlock? block = diff.DiffBlocks[pieceIndex];

            // Calculate hunk header
            int originalStart = Math.Max(1, originalLineNumber - contextLines);
            int modifiedStart = Math.Max(1, modifiedLineNumber - contextLines);

            // Calculate hunk size
            int originalEnd = originalLineNumber + block.DeleteCountA + contextLines;
            int modifiedEnd = modifiedLineNumber + block.InsertCountB + contextLines;

            int originalCount = originalEnd - originalStart + 1;
            int modifiedCount = modifiedEnd - modifiedStart + 1;

            result.AppendLine($"@@ -{originalStart},{originalCount} +{modifiedStart},{modifiedCount} @@");

            // Add context lines before
            int contextStart = Math.Max(0, originalLineNumber - contextLines - 1);
            for (int i = contextStart; i < originalLineNumber - 1; i++)
            {
                if (i < diff.PiecesOld.Count)
                {
                    result.AppendLine($" {diff.PiecesOld[i]}");
                }
            }

            // Add deleted lines
            for (var i = 0; i < block.DeleteCountA; i++)
            {
                if (originalLineNumber - 1 + i < diff.PiecesOld.Count)
                {
                    result.AppendLine($"-{diff.PiecesOld[originalLineNumber - 1 + i]}");
                }
            }

            // Add inserted lines
            for (var i = 0; i < block.InsertCountB; i++)
            {
                if (modifiedLineNumber - 1 + i < diff.PiecesNew.Count)
                {
                    result.AppendLine($"+{diff.PiecesNew[modifiedLineNumber - 1 + i]}");
                }
            }

            // Add context lines after
            int contextEnd = Math.Min(diff.PiecesOld.Count, originalLineNumber + block.DeleteCountA + contextLines);
            for (int i = originalLineNumber + block.DeleteCountA; i < contextEnd; i++)
            {
                result.AppendLine($" {diff.PiecesOld[i]}");
            }

            originalLineNumber += block.DeleteCountA;
            modifiedLineNumber += block.InsertCountB;
            pieceIndex++;
        }

        return result.ToString();
    }

    private static object CalculateStats(DiffResult diff)
    {
        var linesAdded = 0;
        var linesDeleted = 0;

        foreach (DiffBlock? block in diff.DiffBlocks)
        {
            linesDeleted += block.DeleteCountA;
            linesAdded += block.InsertCountB;
        }

        return new
        {
            lines_added = linesAdded,
            lines_deleted = linesDeleted,
            lines_changed = linesAdded + linesDeleted,
            total_lines_old = diff.PiecesOld.Count,
            total_lines_new = diff.PiecesNew.Count
        };
    }

    private static string GetChangeType(ChangeType changeType)
    {
        return changeType switch
        {
            ChangeType.Unchanged => "unchanged",
            ChangeType.Deleted => "deleted",
            ChangeType.Inserted => "added",
            ChangeType.Modified => "modified",
            _ => "unknown"
        };
    }
}
