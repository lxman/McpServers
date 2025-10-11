using Microsoft.AspNetCore.Mvc;
using DesktopCommander.Services;

namespace DesktopCommander.Controllers;

/// <summary>
/// Advanced file reading operations API
/// </summary>
[ApiController]
[Route("api/file-reading")]
public class AdvancedFileReadingController(
    FileVersionService versionService,
    ILogger<AdvancedFileReadingController> logger) : ControllerBase
{
    /// <summary>
    /// Read a specific line range from a file
    /// </summary>
    [HttpGet("read-range")]
    public async Task<IActionResult> ReadRange(
        [FromQuery] string filePath,
        [FromQuery] int startLine,
        [FromQuery] int endLine)
    {
        try
        {
            filePath = Path.GetFullPath(filePath);
            
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound(new { success = false, error = "File not found", filePath });
            }

            string[] allLines = await System.IO.File.ReadAllLinesAsync(filePath);
            int totalLines = allLines.Length;

            if (startLine < 1 || startLine > totalLines)
            {
                return BadRequest(new { success = false, error = "Invalid start line", totalLines });
            }

            if (endLine < startLine || endLine > totalLines)
            {
                return BadRequest(new { success = false, error = "Invalid end line", totalLines });
            }

            string[] linesToReturn = allLines.Skip(startLine - 1).Take(endLine - startLine + 1).ToArray();
            string content = string.Join(Environment.NewLine, linesToReturn);
            string versionToken = versionService.ComputeVersionToken(filePath);

            return Ok(new
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
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading range from: {Path}", filePath);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Read lines around a specific line number with context
    /// </summary>
    [HttpGet("read-around-line")]
    public async Task<IActionResult> ReadAroundLine(
        [FromQuery] string filePath,
        [FromQuery] int lineNumber,
        [FromQuery] int contextLines = 10)
    {
        try
        {
            filePath = Path.GetFullPath(filePath);
            
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound(new { success = false, error = "File not found", filePath });
            }

            string[] allLines = await System.IO.File.ReadAllLinesAsync(filePath);
            int totalLines = allLines.Length;

            if (lineNumber < 1 || lineNumber > totalLines)
            {
                return BadRequest(new { success = false, error = "Invalid line number", totalLines });
            }

            int startLine = Math.Max(1, lineNumber - contextLines);
            int endLine = Math.Min(totalLines, lineNumber + contextLines);

            string[] linesToReturn = allLines.Skip(startLine - 1).Take(endLine - startLine + 1).ToArray();
            string content = string.Join(Environment.NewLine, linesToReturn);
            string versionToken = versionService.ComputeVersionToken(filePath);

            return Ok(new
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
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading around line in: {Path}", filePath);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Read next chunk of lines from a file for incremental processing
    /// </summary>
    [HttpGet("read-next-chunk")]
    public async Task<IActionResult> ReadNextChunk(
        [FromQuery] string filePath,
        [FromQuery] int startLine,
        [FromQuery] int maxLines = 100)
    {
        try
        {
            filePath = Path.GetFullPath(filePath);
            
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound(new { success = false, error = "File not found", filePath });
            }

            string[] allLines = await System.IO.File.ReadAllLinesAsync(filePath);
            int totalLines = allLines.Length;

            if (startLine < 1 || startLine > totalLines)
            {
                return BadRequest(new { success = false, error = "Invalid start line", totalLines });
            }

            int endLine = Math.Min(totalLines, startLine + maxLines - 1);
            string[] linesToReturn = allLines.Skip(startLine - 1).Take(endLine - startLine + 1).ToArray();
            string content = string.Join(Environment.NewLine, linesToReturn);
            string versionToken = versionService.ComputeVersionToken(filePath);

            bool hasMore = endLine < totalLines;
            int? nextStartLine = hasMore ? endLine + 1 : null;

            return Ok(new
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
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading next chunk from: {Path}", filePath);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }
}
