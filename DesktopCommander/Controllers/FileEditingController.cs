using Microsoft.AspNetCore.Mvc;
using DesktopCommander.Services;
using DesktopCommander.Services.AdvancedFileEditing;
using DesktopCommander.Services.AdvancedFileEditing.Models;

namespace DesktopCommander.Controllers;

/// <summary>
/// Advanced file editing operations API
/// </summary>
[ApiController]
[Route("api/editing")]
public class FileEditingController(
    FileEditor fileEditor,
    EditApprovalService approvalService,
    FileVersionService versionService,
    ILogger<FileEditingController> logger) : ControllerBase
{
    /// <summary>
    /// PHASE 1: Prepare to replace a range of lines
    /// </summary>
    [HttpPost("replace-lines")]
    public async Task<IActionResult> ReplaceFileLines([FromBody] ReplaceFileLinesRequest request)
    {
        try
        {
            string filePath = Path.GetFullPath(request.FilePath);
            
            EditResult result = await fileEditor.PrepareReplaceFileLines(
                filePath,
                request.StartLine,
                request.EndLine,
                request.NewContent,
                request.VersionToken,
                request.CreateBackup);

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error preparing line replacement in: {Path}", request.FilePath);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// PHASE 1: Prepare to insert content after a specific line
    /// </summary>
    [HttpPost("insert-after-line")]
    public async Task<IActionResult> InsertAfterLine([FromBody] InsertAfterLineRequest request)
    {
        try
        {
            string filePath = Path.GetFullPath(request.FilePath);
            
            EditResult result = await fileEditor.PrepareInsertAfterLine(
                filePath,
                request.AfterLine,
                request.Content,
                request.VersionToken,
                request.MaintainIndentation,
                request.CreateBackup);

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error preparing insert in: {Path}", request.FilePath);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// PHASE 1: Prepare to delete a range of lines
    /// </summary>
    [HttpDelete("delete-lines")]
    public async Task<IActionResult> DeleteFileLines([FromBody] DeleteFileLinesRequest request)
    {
        try
        {
            string filePath = Path.GetFullPath(request.FilePath);
            
            EditResult result = await fileEditor.PrepareDeleteLines(
                filePath,
                request.StartLine,
                request.EndLine,
                request.VersionToken,
                request.CreateBackup);

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error preparing line deletion in: {Path}", request.FilePath);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// PHASE 1: Prepare to replace text patterns in a file
    /// </summary>
    [HttpPost("replace-in-file")]
    public async Task<IActionResult> ReplaceInFile([FromBody] ReplaceInFileRequest request)
    {
        try
        {
            string filePath = Path.GetFullPath(request.FilePath);
            
            EditResult result = await fileEditor.PrepareReplaceInFile(
                filePath,
                request.SearchPattern,
                request.ReplaceWith,
                request.VersionToken,
                request.CaseSensitive,
                request.UseRegex,
                request.CreateBackup);

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error preparing text replacement in: {Path}", request.FilePath);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Approve and apply a pending edit
    /// </summary>
    [HttpPost("approve")]
    public async Task<IActionResult> ApproveFileEdit([FromBody] ApproveEditRequest request)
    {
        try
        {
            if (request.Confirmation != "APPROVE")
            {
                return BadRequest(new { success = false, error = "Confirmation must be exactly 'APPROVE'" });
            }

            // Get the pending edit (check if it exists without consuming it yet)
            IReadOnlyList<PendingEdit> pendingEdits = approvalService.GetAllPendingEdits();
            PendingEdit? pendingEdit = pendingEdits.FirstOrDefault(pe => pe.ApprovalToken == request.ApprovalToken);
            
            if (pendingEdit == null)
            {
                logger.LogWarning("Invalid or expired approval token: {Token}", request.ApprovalToken);
                return BadRequest(new { success = false, error = "Invalid or expired approval token. Approval tokens expire after 5 minutes." });
            }

            string fullPath = Path.GetFullPath(pendingEdit.FilePath);

            // Get the current version token to validate the file hasn't changed
            string currentVersionToken = versionService.ComputeVersionToken(fullPath);
            
            EditResult result = await fileEditor.ApplyPendingEdit(request.ApprovalToken, currentVersionToken);
            
            if (!result.Success)
            {
                logger.LogError("Failed to apply edit for file {Path}: {Error}", fullPath, result.ErrorDetails);
                return StatusCode(500, new { success = false, error = result.ErrorDetails });
            }
            
            logger.LogInformation("Successfully applied edit to file: {Path}", fullPath);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error approving edit with token: {Token}", request.ApprovalToken);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Cancel a pending edit
    /// </summary>
    [HttpPost("cancel")]
    public IActionResult CancelPendingEdit([FromBody] CancelEditRequest request)
    {
        try
        {
            bool result = approvalService.CancelPendingEdit(request.ApprovalToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error cancelling edit with token: {Token}", request.ApprovalToken);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// List all pending edits awaiting approval
    /// </summary>
    [HttpGet("pending")]
    public IActionResult ListPendingEdits()
    {
        try
        {
            IReadOnlyList<PendingEdit> result = approvalService.GetAllPendingEdits();
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing pending edits");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Find lines in a file matching a pattern
    /// </summary>
    [HttpPost("find-in-file")]
    public async Task<IActionResult> FindInFile([FromBody] FindInFileRequest request)
    {
        try
        {
            string filePath = Path.GetFullPath(request.FilePath);
            
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound(new { success = false, error = "File not found" });
            }

            string[] allLines = await System.IO.File.ReadAllLinesAsync(filePath);
            var matches = new List<object>();

            for (var i = 0; i < allLines.Length; i++)
            {
                var isMatch = false;
                
                if (request.UseRegex)
                {
                    var regex = new System.Text.RegularExpressions.Regex(
                        request.Pattern,
                        request.CaseSensitive 
                            ? System.Text.RegularExpressions.RegexOptions.None 
                            : System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    isMatch = regex.IsMatch(allLines[i]);
                }
                else
                {
                    StringComparison comparison = request.CaseSensitive 
                        ? StringComparison.Ordinal 
                        : StringComparison.OrdinalIgnoreCase;
                    isMatch = allLines[i].Contains(request.Pattern, comparison);
                }

                if (isMatch)
                {
                    matches.Add(new
                    {
                        lineNumber = i + 1,
                        content = allLines[i]
                    });
                }
            }

            return Ok(new
            {
                success = true,
                filePath,
                pattern = request.Pattern,
                matchesFound = matches.Count,
                matches
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching in file: {Path}", request.FilePath);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Analyze indentation in a file
    /// </summary>
    [HttpGet("analyze-indentation")]
    public async Task<IActionResult> AnalyzeIndentation([FromQuery] string filePath)
    {
        try
        {
            filePath = Path.GetFullPath(filePath);
            
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound(new { success = false, error = "File not found" });
            }

            string[] lines = await System.IO.File.ReadAllLinesAsync(filePath);
            var spacesCount = 0;
            var tabsCount = 0;
            var mixedCount = 0;

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                
                char[] leadingWhitespace = line.TakeWhile(char.IsWhiteSpace).ToArray();
                if (leadingWhitespace.Length == 0) continue;

                bool hasSpaces = leadingWhitespace.Contains(' ');
                bool hasTabs = leadingWhitespace.Contains('\t');

                if (hasSpaces && hasTabs)
                    mixedCount++;
                else if (hasSpaces)
                    spacesCount++;
                else if (hasTabs)
                    tabsCount++;
            }

            string recommendation = mixedCount > 0 
                ? "Mixed indentation detected - should standardize"
                : spacesCount > tabsCount 
                    ? "Predominantly spaces - continue using spaces"
                    : "Predominantly tabs - continue using tabs";

            return Ok(new
            {
                success = true,
                filePath,
                analysis = new
                {
                    linesWithSpaces = spacesCount,
                    linesWithTabs = tabsCount,
                    linesWithMixed = mixedCount,
                    recommendation
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error analyzing indentation: {Path}", filePath);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Clean up backup files
    /// </summary>
    [HttpDelete("cleanup-backups")]
    public IActionResult CleanupBackupFiles(
        [FromQuery] string directoryPath,
        [FromQuery] int olderThanHours = 24,
        [FromQuery] string pattern = "*.backup.*")
    {
        try
        {
            directoryPath = Path.GetFullPath(directoryPath);
            
            if (!Directory.Exists(directoryPath))
            {
                return NotFound(new { success = false, error = "Directory not found" });
            }

            DateTime cutoffTime = DateTime.Now.AddHours(-olderThanHours);
            List<string> backupFiles = Directory.GetFiles(directoryPath, pattern, SearchOption.AllDirectories)
                .Where(f => olderThanHours == 0 || System.IO.File.GetLastWriteTime(f) < cutoffTime)
                .ToList();

            foreach (string file in backupFiles)
            {
                System.IO.File.Delete(file);
            }

            return Ok(new
            {
                success = true,
                directoryPath,
                filesDeleted = backupFiles.Count,
                message = $"Deleted {backupFiles.Count} backup files"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error cleaning up backups in: {Path}", directoryPath);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }
}

// Request models
public record ReplaceFileLinesRequest(
    string FilePath,
    int StartLine,
    int EndLine,
    string NewContent,
    string VersionToken,
    bool CreateBackup = false);

public record InsertAfterLineRequest(
    string FilePath,
    int AfterLine,
    string Content,
    string VersionToken,
    bool MaintainIndentation = true,
    bool CreateBackup = false);

public record DeleteFileLinesRequest(
    string FilePath,
    int StartLine,
    int EndLine,
    string VersionToken,
    bool CreateBackup = false);

public record ReplaceInFileRequest(
    string FilePath,
    string SearchPattern,
    string ReplaceWith,
    string VersionToken,
    bool CaseSensitive = false,
    bool UseRegex = false,
    bool CreateBackup = false);

public record ApproveEditRequest(string ApprovalToken, string Confirmation);
public record CancelEditRequest(string ApprovalToken);
public record FindInFileRequest(
    string FilePath,
    string Pattern,
    bool CaseSensitive = false,
    bool UseRegex = false);