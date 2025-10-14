using System.ComponentModel;
using System.Text.Json;
using DesktopCommanderMcp.Common;
using DesktopCommanderMcp.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DesktopCommanderMcp.McpTools;

/// <summary>
/// MCP tools for advanced file reading operations
/// </summary>
[McpServerToolType]
public class AdvancedFileReadingTools(
    FileVersionService versionService,
    ILogger<AdvancedFileReadingTools> logger)
{
    [McpServerTool, DisplayName("read_file_range")]
    [Description("Read a specific line range from a file")]
    public async Task<string> ReadRange(
        [Description("Full path to the file")] string filePath,
        [Description("Starting line number (1-based)")] int startLine,
        [Description("Ending line number (inclusive)")] int endLine)
    {
        try
        {
            filePath = Path.GetFullPath(filePath);
            
            if (!File.Exists(filePath))
            {
                return JsonSerializer.Serialize(new { success = false, error = "File not found", filePath }, 
                    SerializerOptions.JsonOptionsIndented);
            }

            string[] allLines = await File.ReadAllLinesAsync(filePath);
            int totalLines = allLines.Length;

            if (startLine < 1 || startLine > totalLines)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Invalid start line", totalLines }, 
                    SerializerOptions.JsonOptionsIndented);
            }

            if (endLine < startLine || endLine > totalLines)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Invalid end line", totalLines }, 
                    SerializerOptions.JsonOptionsIndented);
            }

            string[] linesToReturn = allLines.Skip(startLine - 1).Take(endLine - startLine + 1).ToArray();
            string content = string.Join(Environment.NewLine, linesToReturn);
            string versionToken = FileVersionService.ComputeVersionToken(filePath);

            return JsonSerializer.Serialize(new
            {
                success = true,
                filePath,
                totalLines,
                startLine,
                endLine,
                linesReturned = linesToReturn.Length,
                content,
                versionToken,
                message = $"Read lines {startLine}-{endLine} of {totalLines}"
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading range from: {Path}", filePath);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, 
                SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("read_around_line")]
    [Description("Read lines around a specific line number with context")]
    public async Task<string> ReadAroundLine(
        [Description("Full path to the file")] string filePath,
        [Description("Target line number")] int lineNumber,
        [Description("Number of context lines before and after (default: 10)")] int contextLines = 10)
    {
        try
        {
            filePath = Path.GetFullPath(filePath);
            
            if (!File.Exists(filePath))
            {
                return JsonSerializer.Serialize(new { success = false, error = "File not found", filePath }, 
                    SerializerOptions.JsonOptionsIndented);
            }

            string[] allLines = await File.ReadAllLinesAsync(filePath);
            int totalLines = allLines.Length;

            if (lineNumber < 1 || lineNumber > totalLines)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Invalid line number", totalLines }, 
                    SerializerOptions.JsonOptionsIndented);
            }

            int startLine = Math.Max(1, lineNumber - contextLines);
            int endLine = Math.Min(totalLines, lineNumber + contextLines);

            string[] linesToReturn = allLines.Skip(startLine - 1).Take(endLine - startLine + 1).ToArray();
            string content = string.Join(Environment.NewLine, linesToReturn);
            string versionToken = FileVersionService.ComputeVersionToken(filePath);

            return JsonSerializer.Serialize(new
            {
                success = true,
                filePath,
                totalLines,
                targetLine = lineNumber,
                contextLines,
                startLine,
                endLine,
                linesReturned = linesToReturn.Length,
                content,
                versionToken,
                message = $"Read {contextLines} lines around line {lineNumber}"
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading around line in: {Path}", filePath);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, 
                SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("read_next_chunk")]
    [Description("Read next chunk of lines from a file for incremental processing")]
    public async Task<string> ReadNextChunk(
        [Description("Full path to the file")] string filePath,
        [Description("Starting line number (1-based)")] int startLine,
        [Description("Maximum number of lines to return (default: 100)")] int maxLines = 100)
    {
        try
        {
            filePath = Path.GetFullPath(filePath);
            
            if (!File.Exists(filePath))
            {
                return JsonSerializer.Serialize(new { success = false, error = "File not found", filePath }, 
                    SerializerOptions.JsonOptionsIndented);
            }

            string[] allLines = await File.ReadAllLinesAsync(filePath);
            int totalLines = allLines.Length;

            if (startLine < 1 || startLine > totalLines)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Invalid start line", totalLines }, 
                    SerializerOptions.JsonOptionsIndented);
            }

            int endLine = Math.Min(totalLines, startLine + maxLines - 1);
            string[] linesToReturn = allLines.Skip(startLine - 1).Take(endLine - startLine + 1).ToArray();
            string content = string.Join(Environment.NewLine, linesToReturn);
            string versionToken = FileVersionService.ComputeVersionToken(filePath);

            bool hasMore = endLine < totalLines;
            int? nextStartLine = hasMore ? endLine + 1 : null;

            return JsonSerializer.Serialize(new
            {
                success = true,
                filePath,
                totalLines,
                startLine,
                endLine,
                linesReturned = linesToReturn.Length,
                content,
                versionToken,
                hasMore,
                nextStartLine,
                message = hasMore 
                    ? $"Read lines {startLine}-{endLine}. Use startLine={nextStartLine} for next chunk."
                    : $"Read final chunk (lines {startLine}-{endLine})"
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading next chunk from: {Path}", filePath);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, 
                SerializerOptions.JsonOptionsIndented);
        }
    }
}