using DesktopCommander.Services;
using Microsoft.AspNetCore.Mvc;

namespace DesktopCommander.Controllers;

/// <summary>
/// File system operations API
/// </summary>
[ApiController]
[Route("api/filesystem")]
public class FileSystemController(
    FileVersionService versionService,
    SecurityManager securityManager,
    AuditLogger auditLogger,
    ILogger<FileSystemController> logger) : ControllerBase
{
    /// <summary>
    /// Read file contents with automatic pagination
    /// </summary>
    [HttpGet("read")]
    public async Task<IActionResult> ReadFile(
        [FromQuery] string path,
        [FromQuery] int? startLine = null,
        [FromQuery] int maxLines = 500)
    {
        try
        {
            path = Path.GetFullPath(path);
            
            if (!System.IO.File.Exists(path))
            {
                return NotFound(new { success = false, error = "File not found", path });
            }
            
            string versionToken = versionService.ComputeVersionToken(path);
            var fileInfo = new FileInfo(path);
            string[] allLines = await System.IO.File.ReadAllLinesAsync(path);
            int totalLines = allLines.Length;

            maxLines = Math.Clamp(maxLines, 1, 1000);
            
            int start = startLine.HasValue ? startLine.Value - 1 : 0;
            int end = Math.Min(start + maxLines, totalLines);
            
            if (start >= totalLines)
            {
                return BadRequest(new 
                { 
                    success = false, 
                    error = "Start line is beyond end of file",
                    totalLines,
                    requestedStartLine = startLine
                });
            }
            
            string[] linesToReturn = allLines.Skip(start).Take(end - start).ToArray();
            string content = string.Join(Environment.NewLine, linesToReturn);
            
            var result = new
            {
                success = true,
                file = new { path, totalLines, sizeBytes = fileInfo.Length },
                content,
                linesReturned = linesToReturn.Length,
                startLine = start + 1,
                endLine = end,
                isTruncated = end < totalLines,
                nextStartLine = end < totalLines ? (int?)(end + 1) : null,
                versionToken,
                message = end < totalLines 
                    ? $"File has {totalLines} lines. Showing lines {start + 1}-{end}. Use startLine={end + 1} to continue reading."
                    : $"Complete file contents ({totalLines} lines)."
            };
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading file: {Path}", path);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Write content to a file
    /// </summary>
    [HttpPost("write")]
    public async Task<IActionResult> WriteFile([FromBody] WriteFileRequest request)
    {
        try
        {
            string path = Path.GetFullPath(request.Path);
            
            if (!securityManager.IsDirectoryAllowed(path))
            {
                return StatusCode(403, new { success = false, error = "Access denied to path" });
            }

            bool fileExists = System.IO.File.Exists(path);
            
            switch (fileExists)
            {
                case true when request.Mode == "overwrite" && string.IsNullOrEmpty(request.VersionToken):
                    return BadRequest(new { success = false, error = "Version token is required for overwriting existing files" });
                case true when request.Mode == "overwrite":
                {
                    string currentToken = versionService.ComputeVersionToken(path);
                    if (currentToken != request.VersionToken)
                    {
                        return Conflict(new { success = false, error = "File has been modified since last read" });
                    }

                    break;
                }
            }

            if (request.Mode == "append")
            {
                await System.IO.File.AppendAllTextAsync(path, request.Content);
            }
            else
            {
                await System.IO.File.WriteAllTextAsync(path, request.Content);
            }
            
            string newToken = versionService.ComputeVersionToken(path);
            
            auditLogger.LogFileOperation(
                fileExists ? "Overwritten" : "Created",
                path,
                true);
            
            return Ok(new
            {
                success = true,
                path,
                bytesWritten = request.Content.Length,
                versionToken = newToken,
                message = fileExists ? "File updated successfully" : "File created successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error writing file: {Path}", request.Path);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// List directory contents
    /// </summary>
    [HttpGet("list")]
    public IActionResult ListDirectory([FromQuery] string path)
    {
        try
        {
            path = Path.GetFullPath(path);
            
            if (!Directory.Exists(path))
            {
                return NotFound(new { success = false, error = "Directory not found", path });
            }

            var directories = Directory.GetDirectories(path)
                .Select(d => new
                {
                    name = Path.GetFileName(d),
                    type = "directory",
                    path = d,
                    modified = Directory.GetLastWriteTime(d)
                })
                .OrderBy(d => d.name).ToList();

            var files = Directory.GetFiles(path)
                .Select(f => new FileInfo(f))
                .Select(f => new
                {
                    name = f.Name,
                    type = "file",
                    path = f.FullName,
                    size = f.Length,
                    modified = f.LastWriteTime
                })
                .OrderBy(f => f.name).ToList();

            var result = new
            {
                success = true,
                path,
                items = directories.Concat<object>(files).ToList(),
                directoryCount = directories.Count,
                fileCount = files.Count
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing directory: {Path}", path);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Delete a file or directory
    /// </summary>
    [HttpDelete("delete")]
    public IActionResult DeletePath([FromQuery] string path, [FromQuery] bool force = false)
    {
        try
        {
            path = Path.GetFullPath(path);
            
            if (!securityManager.IsDirectoryAllowed(path))
            {
                return StatusCode(403, new { success = false, error = "Access denied to path" });
            }

            bool isDirectory = Directory.Exists(path);
            bool isFile = System.IO.File.Exists(path);

            switch (isDirectory)
            {
                case false when !isFile:
                    return NotFound(new { success = false, error = "Path not found", path });
                case true when !force && Directory.GetFileSystemEntries(path).Length > 0:
                    return BadRequest(new { success = false, error = "Directory is not empty. Use force=true to delete." });
                case true:
                    Directory.Delete(path, force);
                    break;
                default:
                    System.IO.File.Delete(path);
                    break;
            }

            auditLogger.LogOperation(
                isDirectory ? "DirectoryDeleted" : "FileDeleted",
                $"Path: {path}",
                true);

            return Ok(new { success = true, path, type = isDirectory ? "directory" : "file", message = "Deleted successfully" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting path: {Path}", path);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Move or rename a file or directory
    /// </summary>
    [HttpPost("move")]
    public IActionResult MoveFile([FromBody] MoveFileRequest request)
    {
        try
        {
            string sourcePath = Path.GetFullPath(request.SourcePath);
            string destinationPath = Path.GetFullPath(request.DestinationPath);
            
            if (!securityManager.IsDirectoryAllowed(sourcePath) || !securityManager.IsDirectoryAllowed(destinationPath))
            {
                return StatusCode(403, new { success = false, error = "Access denied" });
            }

            bool isDirectory = Directory.Exists(sourcePath);
            bool isFile = System.IO.File.Exists(sourcePath);

            switch (isDirectory)
            {
                case false when !isFile:
                    return NotFound(new { success = false, error = "Source path not found" });
                case true:
                    Directory.Move(sourcePath, destinationPath);
                    break;
                default:
                    System.IO.File.Move(sourcePath, destinationPath);
                    break;
            }

            return Ok(new
            {
                success = true,
                sourcePath,
                destinationPath,
                message = "Moved successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error moving file from {Source} to {Dest}", request.SourcePath, request.DestinationPath);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Search for files by pattern
    /// </summary>
    [HttpGet("search")]
    public IActionResult SearchFiles(
        [FromQuery] string searchPath,
        [FromQuery] string pattern,
        [FromQuery] bool recursive = true)
    {
        try
        {
            searchPath = Path.GetFullPath(searchPath);
            
            if (!Directory.Exists(searchPath))
            {
                return NotFound(new { success = false, error = "Search path not found" });
            }

            SearchOption searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.GetFiles(searchPath, pattern, searchOption)
                .Select(f => new FileInfo(f))
                .Select(f => new
                {
                    name = f.Name,
                    path = f.FullName,
                    size = f.Length,
                    modified = f.LastWriteTime
                })
                .ToList();

            return Ok(new
            {
                success = true,
                searchPath,
                pattern,
                recursive,
                filesFound = files.Count,
                files
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching files in {Path}", searchPath);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Get file or directory information
    /// </summary>
    [HttpGet("info")]
    public IActionResult GetFileInfo([FromQuery] string path)
    {
        try
        {
            path = Path.GetFullPath(path);
            
            if (Directory.Exists(path))
            {
                var dirInfo = new DirectoryInfo(path);
                return Ok(new
                {
                    success = true,
                    path,
                    type = "directory",
                    name = dirInfo.Name,
                    created = dirInfo.CreationTime,
                    modified = dirInfo.LastWriteTime,
                    accessed = dirInfo.LastAccessTime,
                    attributes = dirInfo.Attributes.ToString()
                });
            }

            if (!System.IO.File.Exists(path)) return NotFound(new { success = false, error = "Path not found", path });
            var fileInfo = new FileInfo(path);
            return Ok(new
            {
                success = true,
                path,
                type = "file",
                name = fileInfo.Name,
                size = fileInfo.Length,
                extension = fileInfo.Extension,
                created = fileInfo.CreationTime,
                modified = fileInfo.LastWriteTime,
                accessed = fileInfo.LastAccessTime,
                attributes = fileInfo.Attributes.ToString(),
                isReadOnly = fileInfo.IsReadOnly
            });

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting file info: {Path}", path);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Create a directory
    /// </summary>
    [HttpPost("directory")]
    public IActionResult CreateDirectory([FromBody] CreateDirectoryRequest request)
    {
        try
        {
            string path = Path.GetFullPath(request.Path);
            
            if (!securityManager.IsDirectoryAllowed(path))
            {
                return StatusCode(403, new { success = false, error = "Access denied" });
            }

            if (Directory.Exists(path))
            {
                return Conflict(new { success = false, error = "Directory already exists" });
            }

            Directory.CreateDirectory(path);

            return Ok(new
            {
                success = true,
                path,
                message = "Directory created successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating directory: {Path}", request.Path);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }
}

// Request models
public record WriteFileRequest(string Path, string Content, string Mode = "overwrite", string? VersionToken = null);
public record MoveFileRequest(string SourcePath, string DestinationPath);
public record CreateDirectoryRequest(string Path);