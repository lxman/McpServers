using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DesktopDriver.Common;
using DesktopDriver.Exceptions;
using DesktopDriver.Models;
using DesktopDriver.Services;
using ModelContextProtocol.Server;

namespace DesktopDriver.Tools;

[McpServerToolType]
public class FileSystemTools(FileVersionService versionService, SecurityManager securityManager, AuditLogger auditLogger)
{
    [McpServerTool]
    [Description("Read the contents of a file with automatic pagination for large files. Files over 500 lines are automatically chunked to avoid timeouts.")]
    public async Task<string> ReadFile(
        [Description("Path to the file to read - must be canonical")]
        string path,
        [Description("Starting line number for pagination (1-based, optional)")]
        int? startLine = null,
        [Description("Maximum number of lines to return (default: 500, max: 1000)")]
        int maxLines = 500)
    {
        try
        {
            // Validate and canonicalize path
            path = Path.GetFullPath(path);
            
            if (!File.Exists(path))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "File not found",
                    path
                }, SerializerOptions.JsonOptionsIndented);
            }

            // Get file info
            var fileInfo = new FileInfo(path);
            string[] allLines = await File.ReadAllLinesAsync(path);
            int totalLines = allLines.Length;
            
            // Clamp maxLines
            maxLines = Math.Clamp(maxLines, 1, 1000);
            
            // Determine which lines to return
            int start = startLine.HasValue ? startLine.Value - 1 : 0; // Convert to 0-based
            int end = Math.Min(start + maxLines, totalLines);
            
            // Handle out of range
            if (start >= totalLines)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Start line is beyond end of file",
                    totalLines,
                    requestedStartLine = startLine
                }, SerializerOptions.JsonOptionsIndented);
            }
            
            // Get the lines for this page
            string[] linesToReturn = allLines.Skip(start).Take(end - start).ToArray();
            string content = string.Join(Environment.NewLine, linesToReturn);
            
            // Generate version token
            string versionToken = GenerateVersionToken(content);
            
            // Build result
            var result = new ReadFileResult
            {
                Content = content,
                TotalLines = totalLines,
                LinesReturned = linesToReturn.Length,
                StartLine = start + 1, // Convert back to 1-based
                EndLine = end,
                IsTruncated = end < totalLines,
                NextStartLine = end < totalLines ? end + 1 : null,
                VersionToken = versionToken,
                FilePath = path,
                FileSizeBytes = fileInfo.Length
            };
            
            // Create a helpful message
            if (result.IsTruncated)
            {
                result.Message = $"File has {totalLines} lines. Showing lines {result.StartLine}-{result.EndLine}. " +
                               $"Use startLine={result.NextStartLine} to continue reading.";
            }
            else if (startLine.HasValue)
            {
                result.Message = $"Showing lines {result.StartLine}-{result.EndLine} of {totalLines}. End of file reached.";
            }
            else if (totalLines > maxLines)
            {
                result.Message = $"File has {totalLines} lines. Showing first {maxLines} lines. " +
                               $"Use startLine={result.NextStartLine} to continue reading.";
            }
            else
            {
                result.Message = $"Complete file contents ({totalLines} lines).";
            }
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                file = new
                {
                    path = result.FilePath,
                    totalLines = result.TotalLines,
                    sizeBytes = result.FileSizeBytes
                },
                content = result.Content,
                linesReturned = result.LinesReturned,
                startLine = result.StartLine,
                endLine = result.EndLine,
                isTruncated = result.IsTruncated,
                nextStartLine = result.NextStartLine,
                versionToken = result.VersionToken,
                message = result.Message
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Error reading file: {ex.Message}",
                path
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    // Helper method to generate a version token (if not already present)
    private static string GenerateVersionToken(string content)
    {
        byte[] hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content));
        return "sha256:" + Convert.ToHexString(hash).ToLower();
    }

    [McpServerTool]
    [Description("""
                 Write content to a file (overwrite or append).

                     ⚠️ For overwriting existing files, version_token is REQUIRED to prevent conflicts.
                     For new files or append mode, version_token is optional.
                 """)]
    public async Task<string> WriteFile(
        [Description("Path to the file to write - must be canonical")] string path,
        [Description("Content to write")] string content,
        [Description("Write mode: overwrite or append")] string mode = "overwrite",
        [Description("Version token (REQUIRED for overwrite mode on existing files)")] string? versionToken = null)
    {
        try
        {
            string fullPath = Path.GetFullPath(path);
            if (!securityManager.IsDirectoryAllowed(Path.GetDirectoryName(fullPath)!))
            {
                var error = $"Access denied to directory: {Path.GetDirectoryName(fullPath)}";
                auditLogger.LogFileOperation("Write", fullPath, false, error);
                return error;
            }

            bool fileExists = File.Exists(fullPath);

            // If overwriting an existing file, require version token
            if (fileExists && mode.ToLowerInvariant() == "overwrite")
            {
                try
                {
                    versionService.ValidateVersionTokenOrThrow(fullPath, versionToken);
                }
                catch (FileConflictException ex)
                {
                    auditLogger.LogFileOperation("Write", fullPath, false, "FILE_CONFLICT");
                    return $"❌ {ex.Message}";
                }
            }

            // Ensure directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            if (mode.ToLowerInvariant() == "append")
            {
                await File.AppendAllTextAsync(fullPath, content);
                auditLogger.LogFileOperation("Append", fullPath, true);
                
                string newToken = versionService.ComputeVersionToken(fullPath);
                return $"✅ Content appended to file: {fullPath}\n🔐 New version token: {newToken}";
            }

            await File.WriteAllTextAsync(fullPath, content);
            auditLogger.LogFileOperation("Write", fullPath, true);
            
            string versionTokenResult = versionService.ComputeVersionToken(fullPath);
            return $"✅ Content written to file: {fullPath}\n🔐 New version token: {versionTokenResult}";
        }
        catch (Exception ex)
        {
            auditLogger.LogFileOperation("Write", path, false, ex.Message);
            return $"Error writing file: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("List files and directories in a path")]
    public string ListDirectory(
        [Description("Directory path to list - must be canonical")] string path)
    {
        try
        {
            string fullPath = Path.GetFullPath(path);
            if (!securityManager.IsDirectoryAllowed(fullPath))
            {
                var error = $"Access denied to directory: {fullPath}";
                auditLogger.LogFileOperation("List", fullPath, false, error);
                return error;
            }

            if (!Directory.Exists(fullPath))
            {
                var error = $"Directory not found: {fullPath}";
                auditLogger.LogFileOperation("List", fullPath, false, error);
                return error;
            }

            var result = new StringBuilder();
            result.AppendLine($"Contents of: {fullPath}\n");

            // List directories first
            string[] directories = Directory.GetDirectories(fullPath);
            foreach (string dir in directories.OrderBy(d => Path.GetFileName(d)))
            {
                result.AppendLine($"[DIR]  {Path.GetFileName(dir)}");
            }

            // Then list files
            string[] files = Directory.GetFiles(fullPath);
            foreach (string file in files.OrderBy(f => Path.GetFileName(f)))
            {
                var fileInfo = new FileInfo(file);
                result.AppendLine($"[FILE] {Path.GetFileName(file)} ({fileInfo.Length} bytes)");
            }

            auditLogger.LogFileOperation("List", fullPath, true);
            return result.ToString();
        }
        catch (Exception ex)
        {
            auditLogger.LogFileOperation("List", path, false, ex.Message);
            return $"Error listing directory: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Create a directory")]
    public string CreateDirectory(
        [Description("Directory path to create - must be canonical")] string path)
    {
        try
        {
            string fullPath = Path.GetFullPath(path);
            if (!securityManager.IsDirectoryAllowed(Path.GetDirectoryName(fullPath)!))
            {
                var error = $"Access denied to parent directory: {Path.GetDirectoryName(fullPath)}";
                auditLogger.LogFileOperation("CreateDir", fullPath, false, error);
                return error;
            }

            Directory.CreateDirectory(fullPath);
            auditLogger.LogFileOperation("CreateDir", fullPath, true);
            return $"Directory created: {fullPath}";
        }
        catch (Exception ex)
        {
            auditLogger.LogFileOperation("CreateDir", path, false, ex.Message);
            return $"Error creating directory: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Move or rename a file or directory")]
    public string MoveFile(
        [Description("Source path - must be canonical")] string sourcePath,
        [Description("Destination path - must be canonical")] string destinationPath)
    {
        try
        {
            string fullSourcePath = Path.GetFullPath(sourcePath);
            string fullDestPath = Path.GetFullPath(destinationPath);

            if (!securityManager.IsDirectoryAllowed(Path.GetDirectoryName(fullSourcePath)!) ||
                !securityManager.IsDirectoryAllowed(Path.GetDirectoryName(fullDestPath)!))
            {
                var error = "Access denied to source or destination directory";
                auditLogger.LogFileOperation("Move", $"{fullSourcePath} -> {fullDestPath}", false, error);
                return error;
            }

            if (File.Exists(fullSourcePath))
            {
                // Ensure destination directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(fullDestPath)!);
                File.Move(fullSourcePath, fullDestPath);
                auditLogger.LogFileOperation("Move", $"{fullSourcePath} -> {fullDestPath}", true);
                return $"File moved: {fullSourcePath} -> {fullDestPath}";
            }

            if (Directory.Exists(fullSourcePath))
            {
                Directory.Move(fullSourcePath, fullDestPath);
                auditLogger.LogFileOperation("Move", $"{fullSourcePath} -> {fullDestPath}", true);
                return $"Directory moved: {fullSourcePath} -> {fullDestPath}";
            }

            {
                var error = $"Source not found: {fullSourcePath}";
                auditLogger.LogFileOperation("Move", $"{fullSourcePath} -> {fullDestPath}", false, error);
                return error;
            }
        }
        catch (Exception ex)
        {
            auditLogger.LogFileOperation("Move", $"{sourcePath} -> {destinationPath}", false, ex.Message);
            return $"Error moving file/directory: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Delete a file or directory")]
    public string DeletePath(
        [Description("Path to delete - must be canonical")] string path,
        [Description("Force delete (for directories with contents)")] bool force = false)
    {
        try
        {
            string fullPath = Path.GetFullPath(path);
            if (!securityManager.IsDirectoryAllowed(Path.GetDirectoryName(fullPath)!))
            {
                var error = $"Access denied to directory: {Path.GetDirectoryName(fullPath)}";
                auditLogger.LogFileOperation("Delete", fullPath, false, error);
                return error;
            }

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                auditLogger.LogFileOperation("Delete", fullPath, true);
                return $"File deleted: {fullPath}";
            }

            if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, force);
                auditLogger.LogFileOperation("Delete", fullPath, true);
                return $"Directory deleted: {fullPath}";
            }

            {
                var error = $"Path not found: {fullPath}";
                auditLogger.LogFileOperation("Delete", fullPath, false, error);
                return error;
            }
        }
        catch (Exception ex)
        {
            auditLogger.LogFileOperation("Delete", path, false, ex.Message);
            return $"Error deleting path: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Search for files by name pattern")]
    public string SearchFiles(
        [Description("Directory to search in - must be canonical")] string searchPath,
        [Description("File name pattern (supports wildcards)")] string pattern,
        [Description("Include subdirectories")] bool recursive = true)
    {
        try
        {
            string fullPath = Path.GetFullPath(searchPath);
            if (!securityManager.IsDirectoryAllowed(fullPath))
            {
                var error = $"Access denied to directory: {fullPath}";
                auditLogger.LogFileOperation("Search", fullPath, false, error);
                return error;
            }

            if (!Directory.Exists(fullPath))
            {
                var error = $"Directory not found: {fullPath}";
                auditLogger.LogFileOperation("Search", fullPath, false, error);
                return error;
            }

            SearchOption searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            string[] files = Directory.GetFiles(fullPath, pattern, searchOption);

            var result = new StringBuilder();
            result.AppendLine($"Search results for '{pattern}' in {fullPath}:");
            result.AppendLine($"Found {files.Length} files:\n");

            foreach (string file in files.OrderBy(f => f))
            {
                var fileInfo = new FileInfo(file);
                result.AppendLine($"{file} ({fileInfo.Length} bytes, {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm})");
            }

            auditLogger.LogFileOperation("Search", fullPath, true);
            return result.ToString();
        }
        catch (Exception ex)
        {
            auditLogger.LogFileOperation("Search", searchPath, false, ex.Message);
            return $"Error searching files: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Get file or directory information")]
    public string GetFileInfo(
        [Description("Path to examine - must be canonical")] string path)
    {
        try
        {
            string fullPath = Path.GetFullPath(path);
            if (!securityManager.IsDirectoryAllowed(Path.GetDirectoryName(fullPath)!))
            {
                var error = $"Access denied to directory: {Path.GetDirectoryName(fullPath)}";
                auditLogger.LogFileOperation("Info", fullPath, false, error);
                return error;
            }

            if (File.Exists(fullPath))
            {
                var fileInfo = new FileInfo(fullPath);
                auditLogger.LogFileOperation("Info", fullPath, true);
                return $"File Information:\n" +
                       $"Path: {fullPath}\n" +
                       $"Size: {fileInfo.Length} bytes\n" +
                       $"Created: {fileInfo.CreationTime:yyyy-MM-dd HH:mm:ss}\n" +
                       $"Modified: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}\n" +
                       $"Accessed: {fileInfo.LastAccessTime:yyyy-MM-dd HH:mm:ss}\n" +
                       $"Attributes: {fileInfo.Attributes}";
            }

            if (Directory.Exists(fullPath))
            {
                var dirInfo = new DirectoryInfo(fullPath);
                int fileCount = dirInfo.GetFiles().Length;
                int subDirCount = dirInfo.GetDirectories().Length;
                
                auditLogger.LogFileOperation("Info", fullPath, true);
                return $"Directory Information:\n" +
                       $"Path: {fullPath}\n" +
                       $"Files: {fileCount}\n" +
                       $"Subdirectories: {subDirCount}\n" +
                       $"Created: {dirInfo.CreationTime:yyyy-MM-dd HH:mm:ss}\n" +
                       $"Modified: {dirInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}\n" +
                       $"Accessed: {dirInfo.LastAccessTime:yyyy-MM-dd HH:mm:ss}\n" +
                       $"Attributes: {dirInfo.Attributes}";
            }

            {
                var error = $"Path not found: {fullPath}";
                auditLogger.LogFileOperation("Info", fullPath, false, error);
                return error;
            }
        }
        catch (Exception ex)
        {
            auditLogger.LogFileOperation("Info", path, false, ex.Message);
            return $"Error getting file info: {ex.Message}";
        }
    }

    private static string[] GetLinesWithOffset(string[] lines, int offset, int length)
    {
        if (offset < 0)
        {
            // Negative offset means read from end (tail behavior)
            int start = Math.Max(0, lines.Length + offset);
            return lines.Skip(start).ToArray();
        }

        // Positive offset means read from start
        return lines.Skip(offset).Take(length).ToArray();
    }
}
