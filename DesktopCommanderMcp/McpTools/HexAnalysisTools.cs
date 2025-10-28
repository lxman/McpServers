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
    [Description("Read bytes in hex format. See binary-operations/SKILL.md")]
    public Task<string> ReadHexBytes(
        string filePath,
        long offset,
        int length,
        string format = "hex-ascii")
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
    [Description("Generate classic hex dump. See binary-operations/SKILL.md")]
    public Task<string> GenerateHexDump(
        string filePath,
        long offset = 0,
        int length = 512,
        int bytesPerLine = 16)
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
    [Description("Search for hex pattern in binary file. See binary-operations/SKILL.md")]
    public Task<string> SearchHexPattern(
        string filePath,
        string hexPattern,
        long startOffset = 0,
        int maxResults = 100)
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
    [Description("Compare binary files byte-by-byte. See binary-operations/SKILL.md")]
    public Task<string> CompareBinaryFiles(
        string file1Path,
        string file2Path,
        long offset = 0,
        int? length = null,
        bool showMatches = false)
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