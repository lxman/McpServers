using System.ComponentModel;
using System.Text.Json;
using DesktopCommanderMcp.Common;
using DesktopCommanderMcp.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DesktopCommanderMcp.McpTools;

/// <summary>
/// MCP tools for file system operations
/// </summary>
[McpServerToolType]
public class FileSystemTools(
    FileVersionService versionService,
    SecurityManager securityManager,
    AuditLogger auditLogger,
    ResponseSizeGuard responseSizeGuard,
    ILogger<FileSystemTools> logger)
{
    [McpServerTool, DisplayName("get_skills_location")]
    public static string GetSkillsLocation()
    {
        string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        string settingsPath = Path.Combine(appDirectory, "appsettings.json");
        string skillsInfo = File.ReadAllText(settingsPath);
        return skillsInfo;
    }
    
    [McpServerTool, DisplayName("read_file")]
    [Description("Read file with pagination. See file-operations/SKILL.md")]
    public async Task<string> ReadFile(
        [Description("Full path to the file")] string path,
        [Description("Starting line number (1-based, optional)")] int? startLine = null,
        [Description("Maximum number of lines to return (1-1000, default 500)")] int maxLines = 500)
    {
        try
        {
            path = Path.GetFullPath(path);
            
            if (!File.Exists(path))
            {
                return JsonSerializer.Serialize(new { success = false, error = "File not found", path }, 
                    SerializerOptions.JsonOptionsIndented);
            }
            
            string versionToken = FileVersionService.ComputeVersionToken(path);
            var fileInfo = new FileInfo(path);
            string[] allLines = await File.ReadAllLinesAsync(path);
            int totalLines = allLines.Length;

            maxLines = Math.Clamp(maxLines, 1, 1000);
            
            int start = startLine.HasValue ? startLine.Value - 1 : 0;
            int end = Math.Min(start + maxLines, totalLines);
            
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
            
            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading file: {Path}", path);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, 
                SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("write_file")]
    [Description("Write content to file. See file-operations/SKILL.md")]
    public async Task<string> WriteFile(
        [Description("Full path to the file")] string path,
        [Description("Content to write")] string content,
        [Description("Write mode: 'overwrite' or 'append' (default: overwrite)")] string mode = "overwrite",
        [Description("Version token for optimistic concurrency (optional)")] string? versionToken = null)
    {
        try
        {
            path = Path.GetFullPath(path);
            
            if (!securityManager.IsDirectoryAllowed(path))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Access denied to path" }, 
                    SerializerOptions.JsonOptionsIndented);
            }

            bool fileExists = File.Exists(path);
            
            if (mode == "overwrite" && fileExists && !string.IsNullOrEmpty(versionToken))
            {
                string currentVersion = FileVersionService.ComputeVersionToken(path);
                if (currentVersion != versionToken)
                {
                    return JsonSerializer.Serialize(new 
                    { 
                        success = false, 
                        error = "Version conflict",
                        message = "File has been modified since you last read it",
                        expectedVersion = versionToken,
                        currentVersion
                    }, SerializerOptions.JsonOptionsIndented);
                }
            }
            
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (mode == "append")
            {
                await File.AppendAllTextAsync(path, content);
            }
            else
            {
                await File.WriteAllTextAsync(path, content);
            }
            
            auditLogger.LogOperation("write_file", path, true);
            string newVersionToken = FileVersionService.ComputeVersionToken(path);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                path,
                bytesWritten = content.Length,
                versionToken = newVersionToken,
                mode,
                created = !fileExists
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error writing file: {Path}", path);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, 
                SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("list_directory")]
    [Description("List directory contents. See file-operations/SKILL.md")]
    public Task<string> ListDirectory(
        [Description("Full path to the directory")] string path,
        [Description("Number of items to skip (for pagination, default: 0)")] int skip = 0,
        [Description("Maximum number of items to return (1-1000, default: 500)")] int take = 500)
    {
        try
        {
            path = Path.GetFullPath(path);

            if (!Directory.Exists(path))
            {
                return Task.FromResult(JsonSerializer.Serialize(
                    new { success = false, error = "Directory not found", path },
                    SerializerOptions.JsonOptionsIndented));
            }

            // Clamp take value to reasonable limits
            take = Math.Clamp(take, 1, 1000);
            skip = Math.Max(0, skip);

            var directoryInfo = new DirectoryInfo(path);
            var allItems = new List<object>();

            // Add directories first
            foreach (DirectoryInfo dir in directoryInfo.GetDirectories())
            {
                allItems.Add(new
                {
                    name = dir.Name,
                    type = "directory",
                    path = dir.FullName,
                    modified = dir.LastWriteTime
                });
            }

            // Then add files
            foreach (FileInfo file in directoryInfo.GetFiles())
            {
                allItems.Add(new
                {
                    name = file.Name,
                    type = "file",
                    path = file.FullName,
                    size = file.Length,
                    modified = file.LastWriteTime
                });
            }

            // Apply pagination
            int totalItems = allItems.Count;
            var paginatedItems = allItems.Skip(skip).Take(take).ToList();
            int itemsReturned = paginatedItems.Count;
            bool hasMore = skip + itemsReturned < totalItems;

            var result = new
            {
                success = true,
                path,
                items = paginatedItems,
                directoryCount = directoryInfo.GetDirectories().Length,
                fileCount = directoryInfo.GetFiles().Length,
                totalItems,
                itemsReturned,
                skip,
                take,
                hasMore,
                nextSkip = hasMore ? (int?)(skip + itemsReturned) : null,
                message = hasMore
                    ? $"Showing {itemsReturned} of {totalItems} items (skip={skip}). Use skip={skip + itemsReturned} to see more."
                    : totalItems > take
                        ? $"Showing items {skip + 1}-{skip + itemsReturned} of {totalItems} total."
                        : $"Showing all {totalItems} items."
            };

            // Check response size before returning
            string jsonResult = JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);

            // Estimate token count (rough approximation: 1 token ≈ 4 characters)
            int estimatedTokens = jsonResult.Length / 4;
            const int maxTokens = 20000; // Safe limit below the 25000 hard limit

            if (estimatedTokens > maxTokens)
            {
                // Response is too large, return helpful error
                var errorResult = new
                {
                    success = false,
                    error = "Response too large",
                    message = $"This directory contains {totalItems} items and the response would exceed the token limit. " +
                             $"Please use pagination with a smaller 'take' parameter (e.g., take=50 or take=100).",
                    totalItems,
                    requestedTake = take,
                    suggestedTake = Math.Max(1, take / 10), // Suggest 10% of current take
                    path
                };
                return Task.FromResult(JsonSerializer.Serialize(errorResult, SerializerOptions.JsonOptionsIndented));
            }

            return Task.FromResult(jsonResult);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing directory: {Path}", path);
            return Task.FromResult(JsonSerializer.Serialize(
                new { success = false, error = ex.Message },
                SerializerOptions.JsonOptionsIndented));
        }
    }

    [McpServerTool, DisplayName("delete_file")]
    [Description("Delete file or directory. See file-operations/SKILL.md")]
    public Task<string> Delete(
        [Description("Full path to the file or directory")] string path,
        [Description("Force deletion (for directories with contents)")] bool force = false)
    {
        try
        {
            path = Path.GetFullPath(path);

            if (!securityManager.IsDirectoryAllowed(path))
            {
                return Task.FromResult(JsonSerializer.Serialize(
                    new { success = false, error = "Access denied to path" }, 
                    SerializerOptions.JsonOptionsIndented));
            }

            bool isDirectory = Directory.Exists(path);
            bool isFile = File.Exists(path);

            if (!isDirectory && !isFile)
            {
                return Task.FromResult(JsonSerializer.Serialize(
                    new { success = false, error = "Path not found" }, 
                    SerializerOptions.JsonOptionsIndented));
            }

            if (isDirectory)
            {
                Directory.Delete(path, force);
            }
            else
            {
                File.Delete(path);
            }

            auditLogger.LogOperation("delete", path, true);

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                path,
                type = isDirectory ? "directory" : "file",
                deleted = true
            }, SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting: {Path}", path);
            return Task.FromResult(JsonSerializer.Serialize(
                new { success = false, error = ex.Message }, 
                SerializerOptions.JsonOptionsIndented));
        }
    }

    [McpServerTool, DisplayName("move_file")]
    [Description("Move or rename file/directory. See file-operations/SKILL.md")]
    public Task<string> Move(
        [Description("Source path")] string sourcePath,
        [Description("Destination path")] string destinationPath)
    {
        try
        {
            sourcePath = Path.GetFullPath(sourcePath);
            destinationPath = Path.GetFullPath(destinationPath);

            if (!securityManager.IsDirectoryAllowed(sourcePath) || 
                !securityManager.IsDirectoryAllowed(destinationPath))
            {
                return Task.FromResult(JsonSerializer.Serialize(
                    new { success = false, error = "Access denied" }, 
                    SerializerOptions.JsonOptionsIndented));
            }

            bool isDirectory = Directory.Exists(sourcePath);
            bool isFile = File.Exists(sourcePath);

            if (!isDirectory && !isFile)
            {
                return Task.FromResult(JsonSerializer.Serialize(
                    new { success = false, error = "Source path not found" }, 
                    SerializerOptions.JsonOptionsIndented));
            }

            if (isDirectory)
            {
                Directory.Move(sourcePath, destinationPath);
            }
            else
            {
                File.Move(sourcePath, destinationPath);
            }

            auditLogger.LogOperation("move", $"{sourcePath} -> {destinationPath}", true);

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                sourcePath,
                destinationPath,
                type = isDirectory ? "directory" : "file"
            }, SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error moving: {Source} to {Dest}", sourcePath, destinationPath);
            return Task.FromResult(JsonSerializer.Serialize(
                new { success = false, error = ex.Message }, 
                SerializerOptions.JsonOptionsIndented));
        }
    }

    [McpServerTool, DisplayName("search_files")]
    [Description("Search files by pattern. Supports pagination for large result sets. See file-operations/SKILL.md")]
    public Task<string> SearchFiles(
        [Description("Directory to search in")] string searchPath,
        [Description("File pattern (e.g., '*.txt', 'test*.cs')")] string pattern,
        [Description("Search recursively in subdirectories")] bool recursive = true,
        [Description("Number of results to skip (for pagination, default: 0)")] int skip = 0,
        [Description("Maximum number of results to return (1-1000, default: 500)")] int maxResults = 500,
        [Description("Return summary only (count + sample + directory breakdown)")] bool summaryOnly = false,
        [Description("Sort results by: 'name', 'size', 'date' (default: 'name')")] string? sortBy = "name")
    {
        try
        {
            searchPath = Path.GetFullPath(searchPath);

            if (!Directory.Exists(searchPath))
            {
                return Task.FromResult(JsonSerializer.Serialize(
                    new { success = false, error = "Directory not found" },
                    SerializerOptions.JsonOptionsIndented));
            }

            // Clamp parameters to safe ranges
            maxResults = Math.Clamp(maxResults, 1, 1000);
            skip = Math.Max(0, skip);

            SearchOption searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            string[] allFiles = Directory.GetFiles(searchPath, pattern, searchOption);

            // Sort files based on sortBy parameter
            IEnumerable<FileInfo> sortedFiles = sortBy?.ToLower() switch
            {
                "size" => allFiles.Select(f => new FileInfo(f)).OrderByDescending(fi => fi.Length),
                "date" => allFiles.Select(f => new FileInfo(f)).OrderByDescending(fi => fi.LastWriteTime),
                _ => allFiles.Select(f => new FileInfo(f)).OrderBy(fi => fi.Name)
            };

            var allResults = sortedFiles.Select(fi => new
            {
                path = fi.FullName,
                name = fi.Name,
                directory = fi.DirectoryName,
                size = fi.Length,
                modified = fi.LastWriteTime
            }).ToArray();

            int totalCount = allResults.Length;

            // Summary mode - return overview with sample
            if (summaryOnly)
            {
                var directoryCounts = allResults
                    .GroupBy(r => r.directory)
                    .Select(g => new { directory = g.Key, count = g.Count() })
                    .OrderByDescending(d => d.count)
                    .Take(10)
                    .ToArray();

                var summary = new
                {
                    success = true,
                    summaryOnly = true,
                    totalFound = totalCount,
                    searchPath,
                    pattern,
                    recursive,
                    sortBy,
                    sample = allResults.Take(10).ToArray(),
                    topDirectories = directoryCounts,
                    suggestion = totalCount > 100
                        ? $"Use maxResults and skip parameters to paginate through all {totalCount} files"
                        : "Call again without summaryOnly to get full results"
                };

                ResponseSizeCheck summaryCheck = responseSizeGuard.CheckResponseSize(summary, "search_files");
                return Task.FromResult(summaryCheck.IsWithinLimit
                    ? summaryCheck.SerializedJson!
                    : JsonSerializer.Serialize(summary, SerializerOptions.JsonOptionsIndented));
            }

            // Paginate results
            var paginatedResults = allResults.Skip(skip).Take(maxResults).ToArray();
            bool hasMore = skip + paginatedResults.Length < totalCount;

            var responseObject = new
            {
                success = true,
                searchPath,
                pattern,
                recursive,
                sortBy,
                totalFound = totalCount,
                returnedCount = paginatedResults.Length,
                skip,
                maxResults,
                hasMore,
                nextSkip = hasMore ? (int?)(skip + paginatedResults.Length) : null,
                files = paginatedResults,
                message = hasMore
                    ? $"Showing {paginatedResults.Length} of {totalCount} files (items {skip + 1}-{skip + paginatedResults.Length}). Use skip={skip + paginatedResults.Length} for next page."
                    : $"Showing all {totalCount} matching files."
            };

            // Check response size before returning
            ResponseSizeCheck sizeCheck = responseSizeGuard.CheckResponseSize(responseObject, "search_files");

            if (!sizeCheck.IsWithinLimit)
            {
                // Even with pagination, response is too large - suggest smaller maxResults or summary
                return Task.FromResult(ResponseSizeGuard.CreateOversizedErrorResponse(
                    sizeCheck,
                    $"Found {totalCount} files, but even {maxResults} results is too large to return.",
                    "Try using summaryOnly=true first to see an overview, or reduce maxResults to 100 or less.",
                    new
                    {
                        totalFound = totalCount,
                        requestedMaxResults = maxResults,
                        suggestedMaxResults = Math.Max(10, maxResults / 5),
                        retryOptions = new object[]
                        {
                            new
                            {
                                option = "summary",
                                description = "Get an overview with sample results and directory breakdown",
                                parameters = new { searchPath, pattern, recursive, summaryOnly = true }
                            },
                            new
                            {
                                option = "smaller_batch",
                                description = "Get results in smaller batches",
                                parameters = new { searchPath, pattern, recursive, maxResults = 100, skip = 0 }
                            },
                            new
                            {
                                option = "narrow_search",
                                description = "Search in a more specific subdirectory",
                                parameters = new { searchPath = searchPath + "\\[subdirectory]", pattern, recursive }
                            }
                        }
                    }));
            }

            return Task.FromResult(sizeCheck.SerializedJson!);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching files: {Path}", searchPath);
            return Task.FromResult(JsonSerializer.Serialize(
                new { success = false, error = ex.Message }, 
                SerializerOptions.JsonOptionsIndented));
        }
    }

    [McpServerTool, DisplayName("get_file_info")]
    [Description("Get file/directory info. See file-operations/SKILL.md")]
    public Task<string> GetFileInfo(
        [Description("Full path to the file or directory")] string path)
    {
        try
        {
            path = Path.GetFullPath(path);

            bool isDirectory = Directory.Exists(path);
            bool isFile = File.Exists(path);

            if (!isDirectory && !isFile)
            {
                return Task.FromResult(JsonSerializer.Serialize(
                    new { success = false, error = "Path not found" }, 
                    SerializerOptions.JsonOptionsIndented));
            }

            object info;

            if (isDirectory)
            {
                var dirInfo = new DirectoryInfo(path);
                info = new
                {
                    success = true,
                    type = "directory",
                    path = dirInfo.FullName,
                    name = dirInfo.Name,
                    created = dirInfo.CreationTime,
                    modified = dirInfo.LastWriteTime,
                    accessed = dirInfo.LastAccessTime,
                    attributes = dirInfo.Attributes.ToString(),
                    exists = dirInfo.Exists
                };
            }
            else
            {
                var fileInfo = new FileInfo(path);
                info = new
                {
                    success = true,
                    type = "file",
                    path = fileInfo.FullName,
                    name = fileInfo.Name,
                    directory = fileInfo.DirectoryName,
                    size = fileInfo.Length,
                    created = fileInfo.CreationTime,
                    modified = fileInfo.LastWriteTime,
                    accessed = fileInfo.LastAccessTime,
                    attributes = fileInfo.Attributes.ToString(),
                    extension = fileInfo.Extension,
                    exists = fileInfo.Exists
                };
            }

            return Task.FromResult(JsonSerializer.Serialize(info, 
                SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting file info: {Path}", path);
            return Task.FromResult(JsonSerializer.Serialize(
                new { success = false, error = ex.Message }, 
                SerializerOptions.JsonOptionsIndented));
        }
    }

    [McpServerTool, DisplayName("create_directory")]
    [Description("Create directory. See file-operations/SKILL.md")]
    public Task<string> CreateDirectory(
        [Description("Full path to the directory to create")] string path)
    {
        try
        {
            path = Path.GetFullPath(path);

            if (!securityManager.IsDirectoryAllowed(path))
            {
                return Task.FromResult(JsonSerializer.Serialize(
                    new { success = false, error = "Access denied to path" }, 
                    SerializerOptions.JsonOptionsIndented));
            }

            if (Directory.Exists(path))
            {
                return Task.FromResult(JsonSerializer.Serialize(
                    new { success = false, error = "Directory already exists" }, 
                    SerializerOptions.JsonOptionsIndented));
            }

            DirectoryInfo dirInfo = Directory.CreateDirectory(path);
            auditLogger.LogOperation("create_directory", path, dirInfo.Exists);

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                path,
                created = true
            }, SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating directory: {Path}", path);
            return Task.FromResult(JsonSerializer.Serialize(
                new { success = false, error = ex.Message }, 
                SerializerOptions.JsonOptionsIndented));
        }
    }
}