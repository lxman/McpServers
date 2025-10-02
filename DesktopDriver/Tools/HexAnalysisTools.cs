using System.ComponentModel;
using System.Text;
using DesktopDriver.Services;
using ModelContextProtocol.Server;

namespace DesktopDriver.Tools;

/// <summary>
/// MCP tools for binary file hex analysis and comparison
/// </summary>
[McpServerToolType]
public class HexAnalysisTools(HexAnalysisService hexService, AuditLogger auditService)
{
    /// <summary>
    /// Read and display bytes from a file in hexadecimal format
    /// </summary>
    [McpServerTool(Name = "read_hex_bytes")]
    [Description("Read bytes from a file and display them in hexadecimal format with optional ASCII representation")]
    public ToolResultContent ReadHexBytes(
        [Description("Path to the file to read - must be canonical")] string filePath,
        [Description("Starting byte offset (0-based)")] long offset,
        [Description("Number of bytes to read")] int length,
        [Description("Optional output format: 'hex' (hex only), 'hex-ascii' (hex with ASCII - default), 'c-array' (C-style byte array)")] string format = "hex-ascii")
    {
        try
        {
            HexFormat hexFormat = format.ToLower() switch
            {
                "hex" => HexFormat.HexOnly,
                "hex-ascii" => HexFormat.HexAscii,
                "c-array" => HexFormat.CArray,
                _ => HexFormat.HexAscii
            };

            HexDumpResult result = hexService.ReadHexBytes(filePath, offset, length, hexFormat);

            auditService.LogOperation(
                "read_hex_bytes",
                $"Read {result.Length} bytes from offset {offset} in {filePath}",
                success: true
            );

            var sb = new StringBuilder();
            sb.AppendLine($"File: {result.FilePath}");
            sb.AppendLine($"File Size: {result.FileSize:N0} bytes");
            sb.AppendLine($"Reading: {result.Length:N0} bytes starting at offset 0x{result.Offset:X8}");
            sb.AppendLine();
            sb.AppendLine(result.FormattedOutput);

            return new ToolResultContent
            {
                Type = "text",
                Text = sb.ToString()
            };
        }
        catch (Exception ex)
        {
            auditService.LogOperation(
                "read_hex_bytes",
                $"Failed to read hex bytes from {filePath}: {ex.Message}",
                success: false
            );

            return new ToolResultContent
            {
                Type = "text",
                Text = $"Error reading hex bytes: {ex.Message}",
                IsError = true
            };
        }
    }

    /// <summary>
    /// Compare two binary files byte-by-byte
    /// </summary>
    [McpServerTool(Name = "compare_binary_files")]
    [Description("Compare two binary files byte-by-byte and identify differences")]
    public ToolResultContent CompareBinaryFiles(
        [Description("Path to the first file - must be canonical")] string file1Path,
        [Description("Path to the second file - must be canonical")] string file2Path,
        [Description("Optional Starting offset for comparison (default: 0)")] long offset = 0,
        [Description("Optional number of bytes to compare (default: entire files)")] int? length = null,
        [Description("Optional show matching bytes in addition to differences (default: false)")] bool showMatches = false)
    {
        try
        {
            BinaryComparisonResult result = hexService.CompareBinaryFiles(
                file1Path,
                file2Path,
                offset,
                length,
                showMatches
            );

            auditService.LogOperation(
                "compare_binary_files",
                $"Compared {result.ComparisonLength} bytes: {result.TotalDifferences} differences, {result.TotalMatches} matches",
                success: true
            );

            var sb = new StringBuilder();
            sb.AppendLine("=== Binary File Comparison ===");
            sb.AppendLine();
            sb.AppendLine($"File 1: {result.File1Path} ({result.File1Size:N0} bytes)");
            sb.AppendLine($"File 2: {result.File2Path} ({result.File2Size:N0} bytes)");
            sb.AppendLine();
            sb.AppendLine($"Comparison Range: {result.ComparisonLength:N0} bytes starting at offset 0x{result.ComparisonOffset:X8}");
            sb.AppendLine($"Total Matches: {result.TotalMatches:N0}");
            sb.AppendLine($"Total Differences: {result.TotalDifferences:N0}");
            
            if (result.File1Size != result.File2Size)
            {
                sb.AppendLine($"⚠️  WARNING: File sizes differ by {Math.Abs(result.File1Size - result.File2Size):N0} bytes");
            }
            
            if (result.TotalDifferences == 0)
            {
                sb.AppendLine();
                sb.AppendLine("✅ Files are IDENTICAL in the compared range");
            }
            else
            {
                sb.AppendLine();
                sb.AppendLine(result.FormattedComparison);

                if (!result.IsTruncated)
                    return new ToolResultContent
                    {
                        Type = "text",
                        Text = sb.ToString()
                    };
                sb.AppendLine();
                sb.AppendLine($"Note: Output truncated. Showing first 50 of {result.TotalDifferences} differences.");
            }

            return new ToolResultContent
            {
                Type = "text",
                Text = sb.ToString()
            };
        }
        catch (Exception ex)
        {
            auditService.LogOperation(
                "compare_binary_files",
                $"Failed to compare files: {ex.Message}",
                success: false
            );

            return new ToolResultContent
            {
                Type = "text",
                Text = $"Error comparing files: {ex.Message}",
                IsError = true
            };
        }
    }

    /// <summary>
    /// Search for a hex pattern in a binary file
    /// </summary>
    [McpServerTool(Name = "search_hex_pattern")]
    [Description("Search for a hexadecimal pattern in a binary file (supports wildcards with ??)")]
    public ToolResultContent SearchHexPattern(
        [Description("Path to the file to search - must be canonical")] string filePath,
        [Description("Hex pattern to search for (e.g., 'D0CF11E0' or 'D0CF??E0' with wildcards)")] string hexPattern,
        [Description("Optional starting offset for search (default: 0)")] long startOffset = 0,
        [Description("Optional maximum number of matches to return (default: 100)")] int maxResults = 100)
    {
        try
        {
            HexSearchResult result = hexService.SearchHexPattern(
                filePath,
                hexPattern,
                startOffset,
                maxResults
            );

            auditService.LogOperation(
                "search_hex_pattern",
                $"Found {result.TotalMatches} matches for pattern '{hexPattern}' in {filePath}",
                success: true
            );

            var sb = new StringBuilder();
            sb.AppendLine("=== Hex Pattern Search ===");
            sb.AppendLine();
            sb.AppendLine($"File: {result.FilePath} ({result.FileSize:N0} bytes)");
            sb.AppendLine($"Pattern: {result.Pattern}");
            sb.AppendLine($"Search Start Offset: 0x{result.StartOffset:X8}");
            sb.AppendLine($"Total Matches: {result.TotalMatches}");
            
            if (result.IsTruncated)
            {
                sb.AppendLine($"⚠️  Results truncated to {maxResults} matches");
            }
            
            if (result.TotalMatches == 0)
            {
                sb.AppendLine();
                sb.AppendLine("No matches found.");
            }
            else
            {
                sb.AppendLine();
                sb.AppendLine("Matches:");
                sb.AppendLine("Offset     Matched Bytes");
                sb.AppendLine("--------   -------------");
                
                foreach (HexMatch match in result.Matches)
                {
                    string matchedHex = string.Join(" ", match.MatchedBytes.Select(b => $"{b:X2}"));
                    sb.AppendLine($"0x{match.Offset:X8}   {matchedHex}");
                }
            }

            return new ToolResultContent
            {
                Type = "text",
                Text = sb.ToString()
            };
        }
        catch (Exception ex)
        {
            auditService.LogOperation(
                "search_hex_pattern",
                $"Failed to search pattern: {ex.Message}",
                success: false
            );

            return new ToolResultContent
            {
                Type = "text",
                Text = $"Error searching pattern: {ex.Message}",
                IsError = true
            };
        }
    }

    /// <summary>
    /// Generate a classic hex dump of a file section
    /// </summary>
    [McpServerTool(Name = "generate_hex_dump")]
    [Description("Generate a classic hex dump of a file section, hex values, and ASCII representation")]
    public ToolResultContent GenerateHexDump(
        [Description("Path to the file - must be canonical")] string filePath,
        [Description("Optional starting offset (default: 0)")] long offset = 0,
        [Description("Optional number of bytes to dump (default: 512)")] int length = 512,
        [Description("Optional bytes per line (default: 16)")] int bytesPerLine = 16)
    {
        try
        {
            string hexDump = hexService.GenerateHexDump(filePath, offset, length, bytesPerLine);

            auditService.LogOperation(
                "generate_hex_dump",
                $"Generated hex dump of {length} bytes from {filePath}",
                success: true
            );

            return new ToolResultContent
            {
                Type = "text",
                Text = hexDump
            };
        }
        catch (Exception ex)
        {
            auditService.LogOperation(
                "generate_hex_dump",
                $"Failed to generate hex dump: {ex.Message}",
                success: false
            );

            return new ToolResultContent
            {
                Type = "text",
                Text = $"Error generating hex dump: {ex.Message}",
                IsError = true
            };
        }
    }
    
    public class ToolResultContent
    {
        public string Type { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public bool IsError { get; set; }
    }
}
