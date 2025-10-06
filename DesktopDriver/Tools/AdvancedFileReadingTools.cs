using System.ComponentModel;
using System.Text;
using DesktopDriver.Services;
using ModelContextProtocol.Server;

namespace DesktopDriver.Tools;

[McpServerToolType]
public class AdvancedFileReadingTools(
    SecurityManager securityManager,
    AuditLogger auditLogger,
    FileVersionService versionService)
{
    [McpServerTool]
    [Description("Read a specific line range from a file with rich metadata and version token for editing")]
    public async Task<string> AdvancedFileReadRange(
        [Description("Path to the file - must be canonical")] string filePath,
        [Description("Starting line number (1-based)")] int startLine,
        [Description("Ending line number (1-based)")] int endLine)
    {
        try
        {
            string fullPath = Path.GetFullPath(filePath);
            if (!securityManager.IsDirectoryAllowed(Path.GetDirectoryName(fullPath)!))
            {
                var error = $"Access denied to directory: {Path.GetDirectoryName(fullPath)}";
                auditLogger.LogFileOperation("ReadRange", fullPath, false, error);
                return error;
            }

            if (!File.Exists(fullPath))
            {
                var error = $"File not found: {fullPath}";
                auditLogger.LogFileOperation("ReadRange", fullPath, false, error);
                return error;
            }

            string[] allLines = await File.ReadAllLinesAsync(fullPath);
            int totalLines = allLines.Length;

            // Validate range
            if (startLine < 1 || startLine > totalLines)
                return $"❌ Start line {startLine} is out of range (1-{totalLines})";

            if (endLine < startLine || endLine > totalLines)
                return $"❌ End line {endLine} is invalid";

            // Compute version token
            string versionToken = versionService.ComputeVersionToken(fullPath);

            // Extract requested lines
            var result = new StringBuilder();
            result.AppendLine($"📄 File: {fullPath}");
            result.AppendLine($"📊 Total lines: {totalLines}");
            result.AppendLine($"📍 Reading lines {startLine}-{endLine}");
            result.AppendLine($"🔐 Version token: {versionToken}");
            result.AppendLine($"\n--- Content ---\n");

            for (int i = startLine - 1; i < endLine; i++)
            {
                result.AppendLine($"{i + 1:D4} | {allLines[i]}");
            }

            auditLogger.LogFileOperation("ReadRange", fullPath, true);
            return result.ToString();
        }
        catch (Exception ex)
        {
            auditLogger.LogFileOperation("ReadRange", filePath, false, ex.Message);
            return $"Error reading range: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Read lines around a specific line number with context and version token for editing. Use find_in_file first to locate code, then read with minimal context")]
    public async Task<string> AdvancedFileReadAroundLine(
        [Description("Path to the file - must be canonical")] string filePath,
        [Description("Target line number (1-based)")] int lineNumber,
        [Description("Number of context lines before and after (default: 10). Larger values increase token consumption")] int contextLines = 10)
    {
        try
        {
            string fullPath = Path.GetFullPath(filePath);
            if (!securityManager.IsDirectoryAllowed(Path.GetDirectoryName(fullPath)!))
            {
                var error = $"Access denied to directory: {Path.GetDirectoryName(fullPath)}";
                auditLogger.LogFileOperation("ReadAround", fullPath, false, error);
                return error;
            }

            if (!File.Exists(fullPath))
            {
                var error = $"File not found: {fullPath}";
                auditLogger.LogFileOperation("ReadAround", fullPath, false, error);
                return error;
            }

            string[] allLines = await File.ReadAllLinesAsync(fullPath);
            int totalLines = allLines.Length;

            if (lineNumber < 1 || lineNumber > totalLines)
                return $"❌ Line {lineNumber} is out of range (1-{totalLines})";

            // Calculate range
            int startLine = Math.Max(1, lineNumber - contextLines);
            int endLine = Math.Min(totalLines, lineNumber + contextLines);

            // Compute version token
            string versionToken = versionService.ComputeVersionToken(fullPath);

            // Extract lines
            var result = new StringBuilder();
            result.AppendLine($"📄 File: {fullPath}");
            result.AppendLine($"📊 Total lines: {totalLines}");
            result.AppendLine($"🎯 Target line: {lineNumber}");
            result.AppendLine($"📍 Showing lines {startLine}-{endLine} (±{contextLines} context)");
            result.AppendLine($"🔐 Version token: {versionToken}");
            result.AppendLine($"\n--- Content ---\n");

            for (int i = startLine - 1; i < endLine; i++)
            {
                string marker = (i + 1 == lineNumber) ? "➤" : " ";
                result.AppendLine($"{marker} {i + 1:D4} | {allLines[i]}");
            }

            auditLogger.LogFileOperation("ReadAround", fullPath, true);
            return result.ToString();
        }
        catch (Exception ex)
        {
            auditLogger.LogFileOperation("ReadAround", filePath, false, ex.Message);
            return $"Error reading around line: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Read next chunk of lines from a file for incremental processing with version token")]
    public async Task<string> AdvancedFileReadNextChunk(
        [Description("Path to the file - must be canonical")] string filePath,
        [Description("Starting line number (1-based)")] int startLine,
        [Description("Maximum number of lines to read")] int maxLines = 100)
    {
        try
        {
            string fullPath = Path.GetFullPath(filePath);
            if (!securityManager.IsDirectoryAllowed(Path.GetDirectoryName(fullPath)!))
            {
                var error = $"Access denied to directory: {Path.GetDirectoryName(fullPath)}";
                auditLogger.LogFileOperation("ReadChunk", fullPath, false, error);
                return error;
            }

            if (!File.Exists(fullPath))
            {
                var error = $"File not found: {fullPath}";
                auditLogger.LogFileOperation("ReadChunk", fullPath, false, error);
                return error;
            }

            string[] allLines = await File.ReadAllLinesAsync(fullPath);
            int totalLines = allLines.Length;

            if (startLine < 1 || startLine > totalLines)
                return $"❌ Start line {startLine} is out of range (1-{totalLines})";

            int endLine = Math.Min(totalLines, startLine + maxLines - 1);
            bool hasMoreChunks = endLine < totalLines;
            int? nextChunkStart = hasMoreChunks ? endLine + 1 : null;

            // Compute version token
            string versionToken = versionService.ComputeVersionToken(fullPath);

            // Extract lines
            var result = new StringBuilder();
            result.AppendLine($"📄 File: {fullPath}");
            result.AppendLine($"📊 Total lines: {totalLines}");
            result.AppendLine($"📍 Chunk: lines {startLine}-{endLine}");
            result.AppendLine($"📦 Chunk size: {endLine - startLine + 1} lines");
            result.AppendLine($"▶️ Has more chunks: {hasMoreChunks}");
            if (nextChunkStart.HasValue)
                result.AppendLine($"⏭️ Next chunk starts at line: {nextChunkStart.Value}");
            result.AppendLine($"🔐 Version token: {versionToken}");
            result.AppendLine($"\n--- Content ---\n");

            for (int i = startLine - 1; i < endLine; i++)
            {
                result.AppendLine($"{i + 1:D4} | {allLines[i]}");
            }

            auditLogger.LogFileOperation("ReadChunk", fullPath, true);
            return result.ToString();
        }
        catch (Exception ex)
        {
            auditLogger.LogFileOperation("ReadChunk", filePath, false, ex.Message);
            return $"Error reading chunk: {ex.Message}";
        }
    }
}