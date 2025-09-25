using System.ComponentModel;
using System.Text;
using DesktopDriver.Services;
using ModelContextProtocol.Server;

namespace DesktopDriver.Tools;

[McpServerToolType]
public class FileSystemTools
{
    private readonly SecurityManager _securityManager;
    private readonly AuditLogger _auditLogger;

    public FileSystemTools(SecurityManager securityManager, AuditLogger auditLogger)
    {
        _securityManager = securityManager;
        _auditLogger = auditLogger;
    }

    [McpServerTool]
    [Description("Read the contents of a file with optional offset and length parameters")]
    public async Task<string> ReadFile(
        [Description("Path to the file to read")] string path,
        [Description("Start line offset (0-based, negative for tail)")] int offset = 0,
        [Description("Maximum number of lines to read")] int length = 1000)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!_securityManager.IsDirectoryAllowed(Path.GetDirectoryName(fullPath)!))
            {
                var error = $"Access denied to directory: {Path.GetDirectoryName(fullPath)}";
                _auditLogger.LogFileOperation("Read", fullPath, false, error);
                return error;
            }

            if (!File.Exists(fullPath))
            {
                var error = $"File not found: {fullPath}";
                _auditLogger.LogFileOperation("Read", fullPath, false, error);
                return error;
            }

            var lines = await File.ReadAllLinesAsync(fullPath);
            var result = GetLinesWithOffset(lines, offset, length);
            
            _auditLogger.LogFileOperation("Read", fullPath, true);
            return $"File: {fullPath}\nLines {offset} to {offset + result.Length - 1} of {lines.Length}:\n\n{string.Join('\n', result)}";
        }
        catch (Exception ex)
        {
            _auditLogger.LogFileOperation("Read", path, false, ex.Message);
            return $"Error reading file: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Write content to a file (overwrite or append)")]
    public async Task<string> WriteFile(
        [Description("Path to the file to write")] string path,
        [Description("Content to write")] string content,
        [Description("Write mode: overwrite or append")] string mode = "overwrite")
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!_securityManager.IsDirectoryAllowed(Path.GetDirectoryName(fullPath)!))
            {
                var error = $"Access denied to directory: {Path.GetDirectoryName(fullPath)}";
                _auditLogger.LogFileOperation("Write", fullPath, false, error);
                return error;
            }

            // Ensure directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            if (mode.ToLowerInvariant() == "append")
            {
                await File.AppendAllTextAsync(fullPath, content);
                _auditLogger.LogFileOperation("Append", fullPath, true);
                return $"Content appended to file: {fullPath}";
            }

            await File.WriteAllTextAsync(fullPath, content);
            _auditLogger.LogFileOperation("Write", fullPath, true);
            return $"Content written to file: {fullPath}";
        }
        catch (Exception ex)
        {
            _auditLogger.LogFileOperation("Write", path, false, ex.Message);
            return $"Error writing file: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("List files and directories in a path")]
    public string ListDirectory(
        [Description("Directory path to list")] string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!_securityManager.IsDirectoryAllowed(fullPath))
            {
                var error = $"Access denied to directory: {fullPath}";
                _auditLogger.LogFileOperation("List", fullPath, false, error);
                return error;
            }

            if (!Directory.Exists(fullPath))
            {
                var error = $"Directory not found: {fullPath}";
                _auditLogger.LogFileOperation("List", fullPath, false, error);
                return error;
            }

            var result = new StringBuilder();
            result.AppendLine($"Contents of: {fullPath}\n");

            // List directories first
            var directories = Directory.GetDirectories(fullPath);
            foreach (var dir in directories.OrderBy(d => Path.GetFileName(d)))
            {
                result.AppendLine($"[DIR]  {Path.GetFileName(dir)}");
            }

            // Then list files
            var files = Directory.GetFiles(fullPath);
            foreach (var file in files.OrderBy(f => Path.GetFileName(f)))
            {
                var fileInfo = new FileInfo(file);
                result.AppendLine($"[FILE] {Path.GetFileName(file)} ({fileInfo.Length} bytes)");
            }

            _auditLogger.LogFileOperation("List", fullPath, true);
            return result.ToString();
        }
        catch (Exception ex)
        {
            _auditLogger.LogFileOperation("List", path, false, ex.Message);
            return $"Error listing directory: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Create a directory")]
    public string CreateDirectory(
        [Description("Directory path to create")] string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!_securityManager.IsDirectoryAllowed(Path.GetDirectoryName(fullPath)!))
            {
                var error = $"Access denied to parent directory: {Path.GetDirectoryName(fullPath)}";
                _auditLogger.LogFileOperation("CreateDir", fullPath, false, error);
                return error;
            }

            Directory.CreateDirectory(fullPath);
            _auditLogger.LogFileOperation("CreateDir", fullPath, true);
            return $"Directory created: {fullPath}";
        }
        catch (Exception ex)
        {
            _auditLogger.LogFileOperation("CreateDir", path, false, ex.Message);
            return $"Error creating directory: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Move or rename a file or directory")]
    public string MoveFile(
        [Description("Source path")] string sourcePath,
        [Description("Destination path")] string destinationPath)
    {
        try
        {
            var fullSourcePath = Path.GetFullPath(sourcePath);
            var fullDestPath = Path.GetFullPath(destinationPath);

            if (!_securityManager.IsDirectoryAllowed(Path.GetDirectoryName(fullSourcePath)!) ||
                !_securityManager.IsDirectoryAllowed(Path.GetDirectoryName(fullDestPath)!))
            {
                var error = "Access denied to source or destination directory";
                _auditLogger.LogFileOperation("Move", $"{fullSourcePath} -> {fullDestPath}", false, error);
                return error;
            }

            if (File.Exists(fullSourcePath))
            {
                // Ensure destination directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(fullDestPath)!);
                File.Move(fullSourcePath, fullDestPath);
                _auditLogger.LogFileOperation("Move", $"{fullSourcePath} -> {fullDestPath}", true);
                return $"File moved: {fullSourcePath} -> {fullDestPath}";
            }

            if (Directory.Exists(fullSourcePath))
            {
                Directory.Move(fullSourcePath, fullDestPath);
                _auditLogger.LogFileOperation("Move", $"{fullSourcePath} -> {fullDestPath}", true);
                return $"Directory moved: {fullSourcePath} -> {fullDestPath}";
            }

            {
                var error = $"Source not found: {fullSourcePath}";
                _auditLogger.LogFileOperation("Move", $"{fullSourcePath} -> {fullDestPath}", false, error);
                return error;
            }
        }
        catch (Exception ex)
        {
            _auditLogger.LogFileOperation("Move", $"{sourcePath} -> {destinationPath}", false, ex.Message);
            return $"Error moving file/directory: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Delete a file or directory")]
    public string DeletePath(
        [Description("Path to delete")] string path,
        [Description("Force delete (for directories with contents)")] bool force = false)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!_securityManager.IsDirectoryAllowed(Path.GetDirectoryName(fullPath)!))
            {
                var error = $"Access denied to directory: {Path.GetDirectoryName(fullPath)}";
                _auditLogger.LogFileOperation("Delete", fullPath, false, error);
                return error;
            }

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                _auditLogger.LogFileOperation("Delete", fullPath, true);
                return $"File deleted: {fullPath}";
            }

            if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, force);
                _auditLogger.LogFileOperation("Delete", fullPath, true);
                return $"Directory deleted: {fullPath}";
            }

            {
                var error = $"Path not found: {fullPath}";
                _auditLogger.LogFileOperation("Delete", fullPath, false, error);
                return error;
            }
        }
        catch (Exception ex)
        {
            _auditLogger.LogFileOperation("Delete", path, false, ex.Message);
            return $"Error deleting path: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Search for files by name pattern")]
    public string SearchFiles(
        [Description("Directory to search in")] string searchPath,
        [Description("File name pattern (supports wildcards)")] string pattern,
        [Description("Include subdirectories")] bool recursive = true)
    {
        try
        {
            var fullPath = Path.GetFullPath(searchPath);
            if (!_securityManager.IsDirectoryAllowed(fullPath))
            {
                var error = $"Access denied to directory: {fullPath}";
                _auditLogger.LogFileOperation("Search", fullPath, false, error);
                return error;
            }

            if (!Directory.Exists(fullPath))
            {
                var error = $"Directory not found: {fullPath}";
                _auditLogger.LogFileOperation("Search", fullPath, false, error);
                return error;
            }

            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.GetFiles(fullPath, pattern, searchOption);

            var result = new StringBuilder();
            result.AppendLine($"Search results for '{pattern}' in {fullPath}:");
            result.AppendLine($"Found {files.Length} files:\n");

            foreach (var file in files.OrderBy(f => f))
            {
                var fileInfo = new FileInfo(file);
                result.AppendLine($"{file} ({fileInfo.Length} bytes, {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm})");
            }

            _auditLogger.LogFileOperation("Search", fullPath, true);
            return result.ToString();
        }
        catch (Exception ex)
        {
            _auditLogger.LogFileOperation("Search", searchPath, false, ex.Message);
            return $"Error searching files: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Get file or directory information")]
    public string GetFileInfo(
        [Description("Path to examine")] string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!_securityManager.IsDirectoryAllowed(Path.GetDirectoryName(fullPath)!))
            {
                var error = $"Access denied to directory: {Path.GetDirectoryName(fullPath)}";
                _auditLogger.LogFileOperation("Info", fullPath, false, error);
                return error;
            }

            if (File.Exists(fullPath))
            {
                var fileInfo = new FileInfo(fullPath);
                _auditLogger.LogFileOperation("Info", fullPath, true);
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
                var fileCount = dirInfo.GetFiles().Length;
                var subDirCount = dirInfo.GetDirectories().Length;
                
                _auditLogger.LogFileOperation("Info", fullPath, true);
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
                _auditLogger.LogFileOperation("Info", fullPath, false, error);
                return error;
            }
        }
        catch (Exception ex)
        {
            _auditLogger.LogFileOperation("Info", path, false, ex.Message);
            return $"Error getting file info: {ex.Message}";
        }
    }

    private static string[] GetLinesWithOffset(string[] lines, int offset, int length)
    {
        if (offset < 0)
        {
            // Negative offset means read from end (tail behavior)
            var start = Math.Max(0, lines.Length + offset);
            return lines.Skip(start).ToArray();
        }

        // Positive offset means read from start
        return lines.Skip(offset).Take(length).ToArray();
    }
}
