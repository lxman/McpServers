using System.ComponentModel;
using System.Text;
using DesktopDriver.Exceptions;
using DesktopDriver.Services;
using ModelContextProtocol.Server;

namespace DesktopDriver.Tools;

[McpServerToolType]
public class FileSystemTools(FileVersionService versionService, SecurityManager securityManager, AuditLogger auditLogger)
{
    [McpServerTool]
    [Description("Read the contents of a file with optional offset and length parameters. Returns version token for safe editing.")]
    public async Task<string> ReadFile(
        [Description("Path to the file to read - must be canonical")] string path,
        [Description("Start line offset (0-based, negative for tail)")] int offset = 0,
        [Description("Maximum number of lines to read")] int length = 1000)
    {
        try
        {
            string fullPath = Path.GetFullPath(path);
            if (!securityManager.IsDirectoryAllowed(Path.GetDirectoryName(fullPath)!))
            {
                var error = $"Access denied to directory: {Path.GetDirectoryName(fullPath)}";
                auditLogger.LogFileOperation("Read", fullPath, false, error);
                return error;
            }

            if (!File.Exists(fullPath))
            {
                var error = $"File not found: {fullPath}";
                auditLogger.LogFileOperation("Read", fullPath, false, error);
                return error;
            }

            string[] lines = await File.ReadAllLinesAsync(fullPath);
            string[] result = GetLinesWithOffset(lines, offset, length);
            
            // Compute version token for optimistic locking
            string versionToken = versionService.ComputeVersionToken(fullPath);
            
            auditLogger.LogFileOperation("Read", fullPath, true);
            
            var output = $"📄 File: {fullPath}\n";
            output += $"📊 Lines {offset} to {offset + result.Length - 1} of {lines.Length}\n";
            output += $"🔐 Version token: {versionToken}\n";
            output += $"💡 Use this token if you need to edit this file\n";
            output += $"\n--- Content ---\n\n";
            output += string.Join('\n', result);
            
            return output;
        }
        catch (Exception ex)
        {
            auditLogger.LogFileOperation("Read", path, false, ex.Message);
            return $"Error reading file: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description(@"Write content to a file (overwrite or append).

    ⚠️ For overwriting existing files, version_token is REQUIRED to prevent conflicts.
    For new files or append mode, version_token is optional.")]
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
