using Microsoft.Extensions.Logging;
using McpCodeEditor.Services.TypeScript;
using System.Text;

namespace McpCodeEditor.Services.FileOperations;

/// <summary>
/// Centralized service for reading TypeScript files with proper encoding, path resolution, and error handling
/// Provides reliable TypeScript file access with intelligent path handling and validation
/// </summary>
public class TypeScriptFileReader(
    TypeScriptFileResolver fileResolver,
    ILogger<TypeScriptFileReader> logger)
{
    /// <summary>
    /// Read a TypeScript file with proper path resolution and encoding detection
    /// </summary>
    public async Task<TypeScriptFileReadResult> ReadFileAsync(
        string filePath, 
        string? basePath = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Resolve the file path using the TypeScriptFileResolver
            string resolvedPath = fileResolver.ResolvePath(filePath, basePath);
            
            // Validate that it's a valid TypeScript file
            if (!fileResolver.IsValidTypeScriptFile(resolvedPath))
            {
                return new TypeScriptFileReadResult
                {
                    Success = false,
                    ErrorMessage = $"Invalid TypeScript file: {resolvedPath}",
                    FilePath = resolvedPath
                };
            }

            // Check if file exists
            if (!File.Exists(resolvedPath))
            {
                return new TypeScriptFileReadResult
                {
                    Success = false,
                    ErrorMessage = $"File not found: {resolvedPath}",
                    FilePath = resolvedPath
                };
            }

            // Get file info
            var fileInfo = new FileInfo(resolvedPath);
            
            // Read file with encoding detection
            var result = new TypeScriptFileReadResult
            {
                Success = true,
                FilePath = resolvedPath,
                FileName = fileInfo.Name,
                FileSize = fileInfo.Length,
                LastModified = fileInfo.LastWriteTime,
                IsReadOnly = fileInfo.IsReadOnly
            };

            // Read content with proper encoding
            result.Content = await ReadFileContentAsync(resolvedPath, cancellationToken);
            result.ContentLength = result.Content.Length;
            result.LineCount = CountLines(result.Content);
            result.Encoding = DetectEncoding(resolvedPath);

            logger.LogDebug("Successfully read TypeScript file: {FilePath} ({FileSize} bytes, {LineCount} lines)",
                resolvedPath, fileInfo.Length, result.LineCount);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading TypeScript file: {FilePath}", filePath);
            return new TypeScriptFileReadResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                FilePath = filePath
            };
        }
    }

    /// <summary>
    /// Read multiple TypeScript files efficiently with path resolution
    /// </summary>
    public async Task<List<TypeScriptFileReadResult>> ReadMultipleFilesAsync(
        IEnumerable<string> filePaths,
        string? basePath = null,
        int maxConcurrency = 10,
        CancellationToken cancellationToken = default)
    {
        var results = new List<TypeScriptFileReadResult>();
        
        try
        {
            var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

            List<string> paths = filePaths.ToList();
            IEnumerable<Task<TypeScriptFileReadResult>> tasks = paths.Select(async filePath =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    return await ReadFileAsync(filePath, basePath, cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            TypeScriptFileReadResult[] allResults = await Task.WhenAll(tasks);
            results.AddRange(allResults);

            logger.LogDebug("Read {FileCount} TypeScript files, {SuccessCount} successful",
                paths.Count, results.Count(r => r.Success));

            return results;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading multiple TypeScript files");
            return results;
        }
    }

    /// <summary>
    /// Verify that a TypeScript file can be read and is accessible
    /// </summary>
    public async Task<bool> CanReadFileAsync(string filePath, string? basePath = null)
    {
        try
        {
            string resolvedPath = fileResolver.ResolvePath(filePath, basePath);
            
            if (!fileResolver.IsValidTypeScriptFile(resolvedPath) || !File.Exists(resolvedPath))
            {
                return false;
            }

            // Try to read a small portion to verify accessibility
            await using var fileStream = new FileStream(resolvedPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var buffer = new byte[1024];
            await fileStream.ReadExactlyAsync(buffer);
            
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cannot read TypeScript file: {FilePath}", filePath);
            return false;
        }
    }

    /// <summary>
    /// Get file information without reading the full content
    /// </summary>
    public TypeScriptFileInfo? GetFileInfo(string filePath, string? basePath = null)
    {
        try
        {
            string resolvedPath = fileResolver.ResolvePath(filePath, basePath);
            
            if (!fileResolver.IsValidTypeScriptFile(resolvedPath) || !File.Exists(resolvedPath))
            {
                return null;
            }

            var fileInfo = new FileInfo(resolvedPath);
            
            return new TypeScriptFileInfo
            {
                FilePath = resolvedPath,
                FileName = fileInfo.Name,
                FileSize = fileInfo.Length,
                LastModified = fileInfo.LastWriteTime,
                IsReadOnly = fileInfo.IsReadOnly,
                Extension = fileInfo.Extension,
                IsTypeScriptDefinition = resolvedPath.EndsWith(".d.ts", StringComparison.OrdinalIgnoreCase),
                IsReactComponent = resolvedPath.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase)
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error getting file info for: {FilePath}", filePath);
            return null;
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Read file content with proper encoding handling
    /// </summary>
    private static async Task<string> ReadFileContentAsync(string filePath, CancellationToken cancellationToken)
    {
        // Try to detect encoding, default to UTF-8
        Encoding encoding = DetectEncoding(filePath);
        
        using var reader = new StreamReader(filePath, encoding, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    /// <summary>
    /// Detect file encoding with fallback to UTF-8
    /// </summary>
    private static Encoding DetectEncoding(string filePath)
    {
        try
        {
            // Read first few bytes to detect BOM
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var bom = new byte[4];
            int bytesRead = fileStream.Read(bom, 0, 4);
            
            // Check for UTF-8 BOM
            if (bytesRead >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
            {
                return Encoding.UTF8;
            }
            
            // Check for UTF-16 LE BOM
            if (bytesRead >= 2 && bom[0] == 0xFF && bom[1] == 0xFE)
            {
                return Encoding.Unicode;
            }
            
            // Check for UTF-16 BE BOM
            if (bytesRead >= 2 && bom[0] == 0xFE && bom[1] == 0xFF)
            {
                return Encoding.BigEndianUnicode;
            }
            
            // Default to UTF-8
            return Encoding.UTF8;
        }
        catch
        {
            // If detection fails, default to UTF-8
            return Encoding.UTF8;
        }
    }

    /// <summary>
    /// Count lines in text content efficiently
    /// </summary>
    private static int CountLines(string content)
    {
        if (string.IsNullOrEmpty(content))
            return 0;
            
        var count = 1;
        var index = 0;
        
        while ((index = content.IndexOf('\n', index)) != -1)
        {
            count++;
            index++;
        }
        
        return count;
    }

    #endregion
}

/// <summary>
/// Result of TypeScript file read operation
/// </summary>
public class TypeScriptFileReadResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int ContentLength { get; set; }
    public int LineCount { get; set; }
    public long FileSize { get; set; }
    public DateTime LastModified { get; set; }
    public bool IsReadOnly { get; set; }
    public Encoding Encoding { get; set; } = Encoding.UTF8;
}

/// <summary>
/// TypeScript file information without content
/// </summary>
public class TypeScriptFileInfo
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime LastModified { get; set; }
    public bool IsReadOnly { get; set; }
    public string Extension { get; set; } = string.Empty;
    public bool IsTypeScriptDefinition { get; set; }
    public bool IsReactComponent { get; set; }
}
