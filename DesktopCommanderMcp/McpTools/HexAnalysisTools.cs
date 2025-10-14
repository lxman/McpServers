using System.ComponentModel;
using System.Text.Json;
using DesktopCommanderMcp.Common;
using DesktopCommanderMcp.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DesktopCommanderMcp.McpTools;

/// <summary>
/// MCP tools for binary file and hex analysis operations
/// </summary>
[McpServerToolType]
public class HexAnalysisTools(
    HexAnalysisService hexService,
    ILogger<HexAnalysisTools> logger)
{
    [McpServerTool, DisplayName("read_hex_bytes")]
    [Description("Read bytes from a file in hexadecimal format")]
    public Task<string> ReadHexBytes(
        [Description("Full path to the file")] string filePath,
        [Description("Byte offset to start reading from")] long offset,
        [Description("Number of bytes to read")] int length,
        [Description("Output format: 'hex-ascii', 'hex-only', or 'ascii-only' (default: 'hex-ascii')")] string format = "hex-ascii")
    {
        try
        {
            filePath = Path.GetFullPath(filePath);
            
            if (!File.Exists(filePath))
            {
                return Task.FromResult(JsonSerializer.Serialize(
                    new { success = false, error = "File not found" }, 
                    SerializerOptions.JsonOptionsIndented));
            }

            HexDumpResult result = hexService.ReadHexBytes(filePath, offset, length, Enum.Parse<HexFormat>(format, true));
            
            return Task.FromResult(JsonSerializer.Serialize(result, 
                SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading hex bytes from: {Path}", filePath);
            return Task.FromResult(JsonSerializer.Serialize(
                new { success = false, error = ex.Message }, 
                SerializerOptions.JsonOptionsIndented));
        }
    }

    [McpServerTool, DisplayName("generate_hex_dump")]
    [Description("Generate a classic hex dump of a file section")]
    public Task<string> GenerateHexDump(
        [Description("Full path to the file")] string filePath,
        [Description("Byte offset to start from (default: 0)")] long offset = 0,
        [Description("Number of bytes to dump (default: 512)")] int length = 512,
        [Description("Number of bytes per line (default: 16)")] int bytesPerLine = 16)
    {
        try
        {
            filePath = Path.GetFullPath(filePath);
            
            if (!File.Exists(filePath))
            {
                return Task.FromResult(JsonSerializer.Serialize(
                    new { success = false, error = "File not found" }, 
                    SerializerOptions.JsonOptionsIndented));
            }

            string result = hexService.GenerateHexDump(filePath, offset, length, bytesPerLine);
            
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                filePath,
                offset,
                length,
                bytesPerLine,
                hexDump = result
            }, SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating hex dump from: {Path}", filePath);
            return Task.FromResult(JsonSerializer.Serialize(
                new { success = false, error = ex.Message }, 
                SerializerOptions.JsonOptionsIndented));
        }
    }

    [McpServerTool, DisplayName("search_hex_pattern")]
    [Description("Search for a hexadecimal pattern in a binary file")]
    public Task<string> SearchHexPattern(
        [Description("Full path to the file")] string filePath,
        [Description("Hex pattern to search for (e.g., '48656C6C6F' for 'Hello')")] string hexPattern,
        [Description("Byte offset to start search from (default: 0)")] long startOffset = 0,
        [Description("Maximum number of results to return (default: 100)")] int maxResults = 100)
    {
        try
        {
            filePath = Path.GetFullPath(filePath);
            
            if (!File.Exists(filePath))
            {
                return Task.FromResult(JsonSerializer.Serialize(
                    new { success = false, error = "File not found" }, 
                    SerializerOptions.JsonOptionsIndented));
            }

            HexSearchResult result = hexService.SearchHexPattern(
                filePath,
                hexPattern,
                startOffset,
                maxResults);

            return Task.FromResult(JsonSerializer.Serialize(result, 
                SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching hex pattern in: {Path}", filePath);
            return Task.FromResult(JsonSerializer.Serialize(
                new { success = false, error = ex.Message }, 
                SerializerOptions.JsonOptionsIndented));
        }
    }

    [McpServerTool, DisplayName("compare_binary_files")]
    [Description("Compare two binary files byte-by-byte")]
    public Task<string> CompareBinaryFiles(
        [Description("Full path to first file")] string file1Path,
        [Description("Full path to second file")] string file2Path,
        [Description("Byte offset to start comparison (default: 0)")] long offset = 0,
        [Description("Number of bytes to compare (null = entire file)")] int? length = null,
        [Description("Include matching bytes in results (default: false)")] bool showMatches = false)
    {
        try
        {
            file1Path = Path.GetFullPath(file1Path);
            file2Path = Path.GetFullPath(file2Path);
            
            if (!File.Exists(file1Path))
            {
                return Task.FromResult(JsonSerializer.Serialize(
                    new { success = false, error = "First file not found" }, 
                    SerializerOptions.JsonOptionsIndented));
            }
            
            if (!File.Exists(file2Path))
            {
                return Task.FromResult(JsonSerializer.Serialize(
                    new { success = false, error = "Second file not found" }, 
                    SerializerOptions.JsonOptionsIndented));
            }

            BinaryComparisonResult result = hexService.CompareBinaryFiles(
                file1Path,
                file2Path,
                offset,
                length,
                showMatches);

            return Task.FromResult(JsonSerializer.Serialize(result, 
                SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error comparing binary files");
            return Task.FromResult(JsonSerializer.Serialize(
                new { success = false, error = ex.Message }, 
                SerializerOptions.JsonOptionsIndented));
        }
    }
}