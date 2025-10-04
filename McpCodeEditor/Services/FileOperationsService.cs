using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.FileSystemGlobbing;

namespace McpCodeEditor.Services;

public class FileOperationsService(
    CodeEditorConfigurationService config,
    TypeResearchAttestationService? attestationService = null)
{
    // Code file extensions that require research attestation
    private static readonly string[] CodeFileExtensions = 
    [
        ".cs", ".py", ".js", ".ts", ".jsx", ".tsx", ".java", ".go", ".rs",
        ".cpp", ".c", ".h", ".hpp", ".kt", ".swift", ".rb", ".php", ".scala"
    ];

    public async Task<object> ReadFileAsync(string path, string encoding = "utf-8")
    {
        try
        {
            string fullPath = ValidateAndResolvePath(path);

            if (!File.Exists(fullPath))
            {
                return new { success = false, error = $"File not found: {path}" };
            }

            var fileInfo = new FileInfo(fullPath);
            if (fileInfo.Length > config.MaxFileSize)
            {
                return new { success = false, error = $"File too large: {fileInfo.Length} bytes (max: {config.MaxFileSize})" };
            }

            if (!IsAllowedExtension(fullPath))
            {
                return new { success = false, error = $"File extension not allowed: {Path.GetExtension(fullPath)}" };
            }

            Encoding encodingObj = GetEncoding(encoding);
            string content = await File.ReadAllTextAsync(fullPath, encodingObj);

            return new
            {
                success = true,
                content = content,
                path = fullPath,
                size = fileInfo.Length,
                last_modified = fileInfo.LastWriteTime,
                encoding = encoding
            };
        }
        catch (Exception ex)
        {
            return new { success = false, error = ex.Message };
        }
    }

    public async Task<object> WriteFileAsync(
        string path, 
        string content, 
        string encoding = "utf-8", 
        bool createDirectories = true,
        string? researchToken = null)
    {
        try
        {
            string fullPath = ValidateAndResolvePath(path);

            // Check if this is a code file that requires research attestation
            if (attestationService != null && IsCodeFile(fullPath))
            {
                (bool isValid, string? error) = attestationService.ValidateAndConsumeToken(
                    researchToken ?? string.Empty, fullPath);

                if (!isValid)
                {
                    return new
                    {
                        success = false,
                        error = error,
                        hint = "Code files require research attestation. Call attest_code_file_research first to get a research token.",
                        codeFileExtensions = CodeFileExtensions
                    };
                }
            }

            if (createDirectories)
            {
                string? directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }

            if (!IsAllowedExtension(fullPath))
            {
                return new { success = false, error = $"File extension not allowed: {Path.GetExtension(fullPath)}" };
            }

            Encoding encodingObj = GetEncoding(encoding);
            await File.WriteAllTextAsync(fullPath, content, encodingObj);

            var fileInfo = new FileInfo(fullPath);

            return new
            {
                success = true,
                path = fullPath,
                size = fileInfo.Length,
                last_modified = fileInfo.LastWriteTime,
                encoding = encoding
            };
        }
        catch (Exception ex)
        {
            return new { success = false, error = ex.Message };
        }
    }

    public async Task<object> ListFilesAsync(string path, bool recursive = false, bool includeHidden = false, string? pattern = null)
    {
        try
        {
            string fullPath = ValidateAndResolvePath(path);

            if (!Directory.Exists(fullPath))
            {
                return new { success = false, error = $"Directory not found: {path}" };
            }

            SearchOption searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var allEntries = new List<object>();

            // Get directories
            IEnumerable<string> directories = Directory.GetDirectories(fullPath, "*", searchOption)
                .Where(dir => includeHidden || !IsHidden(dir))
                .Where(dir => !IsExcludedDirectory(dir));

            foreach (string dir in directories)
            {
                var dirInfo = new DirectoryInfo(dir);
                allEntries.Add(new
                {
                    type = "directory",
                    name = dirInfo.Name,
                    path = GetRelativePath(dir),
                    full_path = dir,
                    last_modified = dirInfo.LastWriteTime,
                    hidden = IsHidden(dir)
                });
            }

            // Get files
            IEnumerable<string> files = Directory.GetFiles(fullPath, "*", searchOption)
                .Where(file => includeHidden || !IsHidden(file))
                .Where(file => IsAllowedExtension(file));

            // Apply pattern matching if specified
            if (!string.IsNullOrEmpty(pattern))
            {
                var matcher = new Matcher();
                matcher.AddInclude(pattern);
                files = files.Where(file => matcher.Match(Path.GetFileName(file)).HasMatches);
            }

            foreach (string file in files)
            {
                var fileInfo = new FileInfo(file);
                allEntries.Add(new
                {
                    type = "file",
                    name = fileInfo.Name,
                    path = GetRelativePath(file),
                    full_path = file,
                    size = fileInfo.Length,
                    extension = fileInfo.Extension,
                    last_modified = fileInfo.LastWriteTime,
                    hidden = IsHidden(file)
                });
            }

            return new
            {
                success = true,
                path = fullPath,
                entries = allEntries.OrderBy(e => ((dynamic)e).type == "directory" ? 0 : 1)
                                   .ThenBy(e => ((dynamic)e).name)
                                   .ToList()
            };
        }
        catch (Exception ex)
        {
            return new { success = false, error = ex.Message };
        }
    }

    public async Task<object> DeleteAsync(string path, bool recursive = false)
    {
        try
        {
            string fullPath = ValidateAndResolvePath(path);

            if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
            {
                return new { success = false, error = $"Path not found: {path}" };
            }

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                return new { success = true, path = fullPath, type = "file" };
            }

            if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, recursive);
                return new { success = true, path = fullPath, type = "directory", recursive = recursive };
            }

            return new { success = false, error = "Unknown error occurred" };
        }
        catch (Exception ex)
        {
            return new { success = false, error = ex.Message };
        }
    }

    public async Task<object> SearchAsync(string query, string path, string filePattern, bool caseSensitive, bool regex, int maxResults)
    {
        try
        {
            string fullPath = ValidateAndResolvePath(path);

            if (!Directory.Exists(fullPath))
            {
                return new { success = false, error = $"Directory not found: {path}" };
            }

            var results = new List<object>();
            RegexOptions searchOptions = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
            Regex? searchRegex = null;

            if (regex)
            {
                try
                {
                    searchRegex = new Regex(query, searchOptions);
                }
                catch (ArgumentException ex)
                {
                    return new { success = false, error = $"Invalid regex pattern: {ex.Message}" };
                }
            }

            var matcher = new Matcher();
            matcher.AddInclude(filePattern);

            IEnumerable<string> files = Directory.GetFiles(fullPath, "*", SearchOption.AllDirectories)
                .Where(file => matcher.Match(Path.GetFileName(file)).HasMatches)
                .Where(file => IsAllowedExtension(file))
                .Where(file => !IsExcludedDirectory(Path.GetDirectoryName(file) ?? ""));

            foreach (string file in files)
            {
                if (results.Count >= maxResults) break;

                try
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.Length > config.MaxFileSize) continue;

                    string content = await File.ReadAllTextAsync(file);
                    string[] lines = content.Split('\n');
                    var fileMatches = new List<object>();

                    for (var lineNumber = 0; lineNumber < lines.Length; lineNumber++)
                    {
                        string line = lines[lineNumber];

                        if (regex && searchRegex != null)
                        {
                            MatchCollection matches = searchRegex.Matches(line);
                            foreach (Match match in matches)
                            {
                                fileMatches.Add(new
                                {
                                    line_number = lineNumber + 1,
                                    line_content = line.TrimEnd('\r'),
                                    match_start = match.Index,
                                    match_length = match.Length,
                                    match_text = match.Value
                                });
                            }
                        }
                        else
                        {
                            StringComparison comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                            int startIndex = line.IndexOf(query, comparison);
                            if (startIndex >= 0)
                            {
                                fileMatches.Add(new
                                {
                                    line_number = lineNumber + 1,
                                    line_content = line.TrimEnd('\r'),
                                    match_start = startIndex,
                                    match_length = query.Length,
                                    match_text = query
                                });
                            }
                        }
                    }

                    if (fileMatches.Count > 0)
                    {
                        results.Add(new
                        {
                            file = GetRelativePath(file),
                            full_path = file,
                            matches = fileMatches
                        });
                    }
                }
                catch (Exception)
                {
                    // Skip files that can't be read
                }
            }

            return new
            {
                success = true,
                query = query,
                path = fullPath,
                file_pattern = filePattern,
                case_sensitive = caseSensitive,
                regex = regex,
                total_matches = results.Count,
                results = results
            };
        }
        catch (Exception ex)
        {
            return new { success = false, error = ex.Message };
        }
    }

    private bool IsCodeFile(string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();
        return CodeFileExtensions.Contains(extension);
    }

    private string ValidateAndResolvePath(string path)
    {
        // Convert to absolute path
        string fullPath = Path.IsPathRooted(path) ? path : Path.Combine(config.DefaultWorkspace, path);
        fullPath = Path.GetFullPath(fullPath);

        // Security check: ensure path is within workspace if restricted
        if (config.Security.RestrictToWorkspace)
        {
            string workspaceFullPath = Path.GetFullPath(config.DefaultWorkspace);
            if (!fullPath.StartsWith(workspaceFullPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException($"Access denied: Path outside workspace: {path}");
            }
        }

        // Check blocked paths
        foreach (string blockedPath in config.Security.BlockedPaths)
        {
            string blockedFullPath = Path.GetFullPath(blockedPath);
            if (fullPath.StartsWith(blockedFullPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException($"Access denied: Blocked path: {path}");
            }
        }

        return fullPath;
    }

    private bool IsAllowedExtension(string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();
        return config.AllowedExtensions.Contains(extension) || string.IsNullOrEmpty(extension);
    }

    private bool IsExcludedDirectory(string path)
    {
        string dirName = Path.GetFileName(path);
        return config.ExcludedDirectories.Contains(dirName);
    }

    private static bool IsHidden(string path)
    {
        try
        {
            FileAttributes attributes = File.GetAttributes(path);
            return (attributes & FileAttributes.Hidden) == FileAttributes.Hidden;
        }
        catch
        {
            return false;
        }
    }

    private string GetRelativePath(string fullPath)
    {
        string workspaceFullPath = Path.GetFullPath(config.DefaultWorkspace);
        if (fullPath.StartsWith(workspaceFullPath, StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetRelativePath(workspaceFullPath, fullPath);
        }
        return fullPath;
    }

    private static Encoding GetEncoding(string encoding)
    {
        return encoding.ToLowerInvariant() switch
        {
            "utf-8" or "utf8" => Encoding.UTF8,
            "ascii" => Encoding.ASCII,
            "unicode" or "utf-16" => Encoding.Unicode,
            "utf-32" => Encoding.UTF32,
            _ => Encoding.UTF8
        };
    }
}
