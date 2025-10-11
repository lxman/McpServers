using Microsoft.AspNetCore.Mvc;
using DesktopCommander.Services;

namespace DesktopCommander.Controllers;

/// <summary>
/// Binary file and hex analysis operations API
/// </summary>
[ApiController]
[Route("api/hex")]
public class HexAnalysisController(
    HexAnalysisService hexService,
    ILogger<HexAnalysisController> logger) : ControllerBase
{
    /// <summary>
    /// Read bytes from a file in hexadecimal format
    /// </summary>
    [HttpGet("read")]
    public IActionResult ReadHexBytes(
        [FromQuery] string filePath,
        [FromQuery] long offset,
        [FromQuery] int length,
        [FromQuery] string format = "hex-ascii")
    {
        try
        {
            filePath = Path.GetFullPath(filePath);
            
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound(new { success = false, error = "File not found" });
            }

            HexDumpResult result = hexService.ReadHexBytes(filePath, offset, length, Enum.Parse<HexFormat>(format));
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading hex bytes from: {Path}", filePath);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Generate a classic hex dump of a file section
    /// </summary>
    [HttpGet("dump")]
    public IActionResult GenerateHexDump(
        [FromQuery] string filePath,
        [FromQuery] long offset = 0,
        [FromQuery] int length = 512,
        [FromQuery] int bytesPerLine = 16)
    {
        try
        {
            filePath = Path.GetFullPath(filePath);
            
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound(new { success = false, error = "File not found" });
            }

            string result = hexService.GenerateHexDump(filePath, offset, length, bytesPerLine);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating hex dump from: {Path}", filePath);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Search for a hexadecimal pattern in a binary file
    /// </summary>
    [HttpPost("search")]
    public IActionResult SearchHexPattern([FromBody] SearchHexPatternRequest request)
    {
        try
        {
            string filePath = Path.GetFullPath(request.FilePath);
            
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound(new { success = false, error = "File not found" });
            }

            HexSearchResult result = hexService.SearchHexPattern(
                filePath,
                request.HexPattern,
                request.StartOffset,
                request.MaxResults);

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching hex pattern in: {Path}", request.FilePath);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Compare two binary files byte-by-byte
    /// </summary>
    [HttpPost("compare")]
    public IActionResult CompareBinaryFiles([FromBody] CompareBinaryFilesRequest request)
    {
        try
        {
            string file1Path = Path.GetFullPath(request.File1Path);
            string file2Path = Path.GetFullPath(request.File2Path);
            
            if (!System.IO.File.Exists(file1Path))
            {
                return NotFound(new { success = false, error = "First file not found" });
            }
            
            if (!System.IO.File.Exists(file2Path))
            {
                return NotFound(new { success = false, error = "Second file not found" });
            }

            BinaryComparisonResult result = hexService.CompareBinaryFiles(
                file1Path,
                file2Path,
                request.Offset,
                request.Length,
                request.ShowMatches);

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error comparing binary files");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }
}

// Request models
public record SearchHexPatternRequest(
    string FilePath,
    string HexPattern,
    long StartOffset = 0,
    int MaxResults = 100);

public record CompareBinaryFilesRequest(
    string File1Path,
    string File2Path,
    long Offset = 0,
    int? Length = null,
    bool ShowMatches = false);
