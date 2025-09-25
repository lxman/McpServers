using System.ComponentModel;
using System.Text.Json;
using DesktopDriver.Services;
using DesktopDriver.Services.Doc;
using DesktopDriver.Services.Doc.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DesktopDriver.Tools;

[McpServerToolType]
public class DocTools(
    SecurityManager securityManager,
    AuditLogger auditLogger,
    PasswordManager passwordManager,
    DocumentProcessor documentProcessor,
    DocumentIndexer documentIndexer,
    ILogger<DocTools> logger)
{
    #region Password Management

    [McpServerTool]
    [Description("Register a password for documents matching a specific pattern")]
    public Task<string> RegisterPasswordPattern(
        [Description("File pattern (e.g., '**/PAY.gov/**', '**/TADERA*')")] string pattern,
        [Description("Password for documents matching this pattern")] string password)
    {
        try
        {
            passwordManager.RegisterPasswordPattern(pattern, password);
            auditLogger.LogPasswordOperation("RegisterPattern", pattern, true);
            return Task.FromResult($"Password pattern registered successfully: {pattern}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to register password pattern: {Pattern}", pattern);
            auditLogger.LogPasswordOperation("RegisterPattern", pattern, false, ex.Message);
            return Task.FromResult($"Failed to register password pattern: {ex.Message}");
        }
    }

    [McpServerTool]
    [Description("Register a password for a specific file")]
    public Task<string> RegisterSpecificPassword(
        [Description("Full path to the file")] string filePath,
        [Description("Password for this specific file")] string password)
    {
        try
        {
            if (!securityManager.IsDirectoryAllowed(Path.GetDirectoryName(filePath)!))
            {
                var error = $"Access denied to directory: {Path.GetDirectoryName(filePath)}";
                auditLogger.LogPasswordOperation("RegisterSpecific", filePath, false, error);
                return Task.FromResult(error);
            }

            passwordManager.RegisterSpecificPassword(filePath, password);
            auditLogger.LogPasswordOperation("RegisterSpecific", filePath, true);
            return Task.FromResult($"Password registered for file: {Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to register specific password: {FilePath}", filePath);
            auditLogger.LogPasswordOperation("RegisterSpecific", filePath, false, ex.Message);
            return Task.FromResult($"Failed to register password: {ex.Message}");
        }
    }

    [McpServerTool]
    [Description("Automatically detect and register passwords from password files (*.txt, *.pwd)")]
    public async Task<string> AutoDetectPasswords(
        [Description("Root directory to search for password files")] string rootPath)
    {
        try
        {
            var fullPath = Path.GetFullPath(rootPath);
            if (!securityManager.IsDirectoryAllowed(fullPath))
            {
                var error = $"Access denied to directory: {fullPath}";
                auditLogger.LogPasswordOperation("AutoDetect", fullPath, false, error);
                return error;
            }

            await passwordManager.AutoDetectPasswordFiles(fullPath);
            auditLogger.LogPasswordOperation("AutoDetect", fullPath, true);
            return $"Password auto-detection completed for: {rootPath}";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to auto-detect passwords: {RootPath}", rootPath);
            auditLogger.LogPasswordOperation("AutoDetect", rootPath, false, ex.Message);
            return $"Failed to auto-detect passwords: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Register multiple password patterns from JSON")]
    public Task<string> BulkRegisterPasswords(
        [Description("JSON object with pattern-password mappings")] string passwordMapJson)
    {
        try
        {
            var passwordMap = JsonSerializer.Deserialize<Dictionary<string, string>>(passwordMapJson);
            if (passwordMap == null)
            {
                return Task.FromResult("Invalid JSON format for password map");
            }

            var registered = 0;
            var failed = 0;

            foreach (var (pattern, password) in passwordMap)
            {
                try
                {
                    passwordManager.RegisterPasswordPattern(pattern, password);
                    registered++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to register pattern: {Pattern}", pattern);
                    failed++;
                }
            }

            var result = $"Bulk password registration completed: {registered} successful, {failed} failed";
            auditLogger.LogPasswordOperation("BulkRegister", passwordMapJson.Substring(0, Math.Min(100, passwordMapJson.Length)), registered > 0);
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to bulk register passwords");
            auditLogger.LogPasswordOperation("BulkRegister", "JSON", false, ex.Message);
            return Task.FromResult($"Failed to bulk register passwords: {ex.Message}");
        }
    }

    #endregion

    #region Document Processing

    [McpServerTool]
    [Description("Extract content from a single document (supports password-protected files)")]
    public async Task<string> ExtractDocumentContent(
        [Description("Path to the document file")] string filePath,
        [Description("Optional password (will try auto-detected passwords first)")] string? password = null)
    {
        try
        {
            var fullPath = Path.GetFullPath(filePath);
            if (!securityManager.IsDirectoryAllowed(Path.GetDirectoryName(fullPath)!))
            {
                var error = $"Access denied to directory: {Path.GetDirectoryName(fullPath)}";
                auditLogger.LogFileOperation("ExtractContent", fullPath, false, error);
                return error;
            }

            if (!File.Exists(fullPath))
            {
                var error = $"File not found: {fullPath}";
                auditLogger.LogFileOperation("ExtractContent", fullPath, false, error);
                return error;
            }

            var content = await documentProcessor.ExtractContent(fullPath, password);
            
            var result = $"Document: {content.FilePath}\n" +
                        $"Type: {content.DocumentType}\n" +
                        $"Title: {content.Title}\n" +
                        $"Size: {content.Metadata.FileSizeBytes:N0} bytes\n" +
                        $"Modified: {content.Metadata.ModifiedDate:yyyy-MM-dd HH:mm:ss}\n" +
                        $"Password Required: {content.RequiredPassword}\n" +
                        $"Extracted: {content.ExtractedAt:yyyy-MM-dd HH:mm:ss}\n\n" +
                        $"Content Preview (first 1000 chars):\n" +
                        $"{content.PlainText.Substring(0, Math.Min(1000, content.PlainText.Length))}";

            if (content.PlainText.Length > 1000)
            {
                result += $"\n\n... (truncated {content.PlainText.Length - 1000} more characters)";
            }

            auditLogger.LogFileOperation("ExtractContent", fullPath, true);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to extract content from: {FilePath}", filePath);
            auditLogger.LogFileOperation("ExtractContent", filePath, false, ex.Message);
            return $"Failed to extract document content: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Get detailed metadata from a document")]
    public async Task<string> GetDocumentMetadata(
        [Description("Path to the document file")] string filePath,
        [Description("Optional password")] string? password = null)
    {
        try
        {
            var fullPath = Path.GetFullPath(filePath);
            if (!securityManager.IsDirectoryAllowed(Path.GetDirectoryName(fullPath)!))
            {
                return $"Access denied to directory: {Path.GetDirectoryName(fullPath)}";
            }

            var content = await documentProcessor.ExtractContent(fullPath, password);
            var metadata = content.Metadata;

            var result = $"File: {metadata.FileName}\n" +
                        $"Path: {content.FilePath}\n" +
                        $"Document Type: {content.DocumentType}\n" +
                        $"Title: {content.Title}\n" +
                        $"Size: {metadata.FileSizeBytes:N0} bytes\n" +
                        $"Created: {metadata.CreatedDate:yyyy-MM-dd HH:mm:ss}\n" +
                        $"Modified: {metadata.ModifiedDate:yyyy-MM-dd HH:mm:ss}\n" +
                        $"Accessed: {metadata.AccessedDate:yyyy-MM-dd HH:mm:ss}\n";

            if (!string.IsNullOrWhiteSpace(metadata.Author))
                result += $"Author: {metadata.Author}\n";
            
            if (!string.IsNullOrWhiteSpace(metadata.Subject))
                result += $"Subject: {metadata.Subject}\n";
            
            if (!string.IsNullOrWhiteSpace(metadata.Keywords))
                result += $"Keywords: {metadata.Keywords}\n";
            
            if (metadata.PageCount > 0)
                result += $"Pages: {metadata.PageCount}\n";
            
            if (metadata.WordCount > 0)
                result += $"Words: {metadata.WordCount:N0}\n";
            
            if (metadata.CharacterCount > 0)
                result += $"Characters: {metadata.CharacterCount:N0}\n";

            if (content.Sections.Count != 0)
                result += $"Sections: {content.Sections.Count}\n";
            
            if (content.StructuredData.Any())
                result += $"Structured Data: {string.Join(", ", content.StructuredData.Keys)}\n";

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get metadata from: {FilePath}", filePath);
            return $"Failed to get document metadata: {ex.Message}";
        }
    }

    #endregion

    #region Document Indexing

    [McpServerTool]
    [Description("Create a searchable index from a directory of documents")]
    public async Task<string> CreateDocumentIndex(
        [Description("Name for the search index")] string indexName,
        [Description("Root directory containing documents to index")] string rootPath,
        [Description("JSON options for indexing (optional)")] string? optionsJson = null)
    {
        try
        {
            var fullPath = Path.GetFullPath(rootPath);
            if (!securityManager.IsDirectoryAllowed(fullPath))
            {
                var error = $"Access denied to directory: {fullPath}";
                auditLogger.LogIndexOperation("CreateIndex", indexName, false, error);
                return error;
            }

            IndexingOptions? options = null;
            if (!string.IsNullOrWhiteSpace(optionsJson))
            {
                try
                {
                    options = JsonSerializer.Deserialize<IndexingOptions>(optionsJson);
                }
                catch (Exception ex)
                {
                    return $"Invalid options JSON: {ex.Message}";
                }
            }

            var result = await documentIndexer.BuildIndex(indexName, fullPath, options);
            
            var response = $"Index '{indexName}' created successfully!\n" +
                          $"Root Path: {result.RootPath}\n" +
                          $"Duration: {result.Duration.TotalMinutes:F1} minutes\n" +
                          $"Successful: {result.Successful.Count:N0} documents\n" +
                          $"Failed: {result.Failed.Count:N0} documents\n" +
                          $"Password Protected: {result.PasswordProtected.Count:N0} documents\n" +
                          $"Total Size: {result.TotalSizeBytes / (1024 * 1024):N1} MB\n\n";

            if (result.FileTypeStats.Count != 0)
            {
                response += "File Types:\n";
                foreach (var (type, count) in result.FileTypeStats.OrderByDescending(x => x.Value))
                {
                    response += $"  {type}: {count:N0}\n";
                }
                response += "\n";
            }

            if (result.Failed.Count != 0)
            {
                response += "Failed Documents (first 5):\n";
                response = result.Failed.Take(5).Aggregate(response, (current, failed) => current + $"  {Path.GetFileName(failed.FilePath)}: {failed.ErrorMessage}\n");
                if (result.Failed.Count > 5)
                {
                    response += $"  ... and {result.Failed.Count - 5} more\n";
                }
            }

            auditLogger.LogIndexOperation("CreateIndex", indexName, true);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create index: {IndexName}", indexName);
            auditLogger.LogIndexOperation("CreateIndex", indexName, false, ex.Message);
            return $"Failed to create index: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Search documents in an index using natural language queries")]
    public string SearchDocuments(
        [Description("Search query (natural language or keywords)")] string query,
        [Description("Name of the index to search")] string indexName,
        [Description("JSON search options (optional)")] string? searchOptionsJson = null)
    {
        try
        {
            SearchQuery? searchQuery = null;
            if (!string.IsNullOrWhiteSpace(searchOptionsJson))
            {
                try
                {
                    searchQuery = JsonSerializer.Deserialize<SearchQuery>(searchOptionsJson);
                    if (searchQuery != null)
                    {
                        searchQuery.Query = query; // Ensure query is set
                    }
                }
                catch (Exception ex)
                {
                    return $"Invalid search options JSON: {ex.Message}";
                }
            }

            var results = documentIndexer.Search(query, indexName, searchQuery);
            
            var response = $"Search Results for: \"{query}\"\n" +
                          $"Index: {indexName}\n" +
                          $"Total Hits: {results.TotalHits:N0}\n" +
                          $"Search Time: {results.SearchTimeMs:F0}ms\n" +
                          $"Showing: {results.Results.Count} results\n\n";

            for (var i = 0; i < results.Results.Count; i++)
            {
                var result = results.Results[i];
                response += $"{i + 1}. {result.Title}\n" +
                           $"   File: {Path.GetFileName(result.FilePath)}\n" +
                           $"   Path: {Path.GetDirectoryName(result.FilePath)}\n" +
                           $"   Type: {result.DocumentType}\n" +
                           $"   Score: {result.RelevanceScore:F2}\n" +
                           $"   Modified: {result.ModifiedDate:yyyy-MM-dd}\n" +
                           $"   Size: {result.FileSizeBytes:N0} bytes\n";

                if (result.Snippets.Count != 0)
                {
                    response += "   Snippets:\n";
                    response = result.Snippets.Take(2).Aggregate(response, (current, snippet) => current + $"     \"{snippet}\"\n");
                }

                response += "\n";
            }

            if (results.FileTypeCounts.Count != 0)
            {
                response += "File Types in Results:\n";
                foreach (var (type, count) in results.FileTypeCounts)
                {
                    response += $"  {type}: {count}\n";
                }
            }

            auditLogger.LogSearchOperation("Search", indexName, query, true, results.TotalHits);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Search failed: {Query} in {IndexName}", query, indexName);
            auditLogger.LogSearchOperation("Search", indexName, query, false);
            return $"Search failed: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("List all available document indexes")]
    public string ListDocumentIndexes()
    {
        try
        {
            var indexes = documentIndexer.GetIndexNames();
            
            if (indexes.Count == 0)
            {
                return "No document indexes found. Use create_document_index to create one.";
            }

            var response = $"Available Document Indexes ({indexes.Count}):\n\n";

            return indexes.OrderBy(x => x).Aggregate(response, (current, indexName) => current + $"• {indexName}\n");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list indexes");
            return $"Failed to list indexes: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Remove a document index")]
    public string RemoveDocumentIndex(
        [Description("Name of the index to remove")] string indexName)
    {
        try
        {
            var removed = documentIndexer.RemoveIndex(indexName);
            
            if (removed)
            {
                auditLogger.LogIndexOperation("RemoveIndex", indexName, true);
                return $"Index '{indexName}' removed successfully.";
            }

            return $"Index '{indexName}' not found.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to remove index: {IndexName}", indexName);
            auditLogger.LogIndexOperation("RemoveIndex", indexName, false, ex.Message);
            return $"Failed to remove index: {ex.Message}";
        }
    }
    
    [McpServerTool]
    [Description("Remove duplicate documents from an index")]
    public string DeduplicateIndex(
        [Description("Name of the index to deduplicate")] string indexName)
    {
        try
        {
            var docCount = documentIndexer.DeduplicateIndex(indexName);
            return $"Deduplication completed. Index '{indexName}' now contains {docCount} unique documents.";
        }
        catch (Exception ex)
        {
            return $"Deduplication failed: {ex.Message}";
        }
    }

    #endregion

    #region Utility Functions

    [McpServerTool]
    [Description("Discover documents in a directory (without indexing)")]
    public Task<string> DiscoverDocuments(
        [Description("Root directory to search")] string rootPath,
        [Description("File patterns to include (comma-separated, e.g., '*.pdf,*.docx')")] string includePatterns = "*",
        [Description("Whether to search subdirectories")] bool recursive = true)
    {
        try
        {
            var fullPath = Path.GetFullPath(rootPath);
            if (!securityManager.IsDirectoryAllowed(fullPath))
            {
                return Task.FromResult($"Access denied to directory: {fullPath}");
            }

            if (!Directory.Exists(fullPath))
            {
                return Task.FromResult($"Directory not found: {fullPath}");
            }

            var patterns = includePatterns.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim()).ToArray();
            
            var documents = new List<FileInfo>();
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            
            foreach (var pattern in patterns)
            {
                var files = Directory.GetFiles(fullPath, pattern, searchOption);
                documents.AddRange(files.Select(f => new FileInfo(f)));
            }

            documents = documents.DistinctBy(d => d.FullName).OrderBy(d => d.FullName).ToList();
            
            var response = $"Document Discovery Results\n" +
                          $"Directory: {fullPath}\n" +
                          $"Patterns: {string.Join(", ", patterns)}\n" +
                          $"Recursive: {recursive}\n" +
                          $"Found: {documents.Count:N0} documents\n\n";

            var typeGroups = documents.GroupBy(d => d.Extension.ToLowerInvariant())
                .OrderByDescending(g => g.Count());
            
            response += "File Types:\n";
            foreach (var group in typeGroups)
            {
                var extension = string.IsNullOrEmpty(group.Key) ? "(no extension)" : group.Key;
                response += $"  {extension}: {group.Count():N0} files\n";
            }

            var totalSize = documents.Sum(d => d.Length);
            response += $"\nTotal Size: {totalSize / (1024.0 * 1024):F1} MB\n";

            if (documents.Count <= 20)
            {
                response += "\nFiles:\n";
                foreach (var doc in documents)
                {
                    response += $"  {Path.GetRelativePath(fullPath, doc.FullName)} " +
                               $"({doc.Length:N0} bytes, {doc.LastWriteTime:yyyy-MM-dd})\n";
                }
            }
            else
            {
                response += $"\n(Showing file types only - {documents.Count} total files)";
            }

            return Task.FromResult(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to discover documents: {RootPath}", rootPath);
            return Task.FromResult($"Failed to discover documents: {ex.Message}");
        }
    }

    [McpServerTool]
    [Description("Get registered password patterns (passwords are masked)")]
    public Task<string> GetRegisteredPasswords()
    {
        try
        {
            var patterns = passwordManager.GetRegisteredPatterns();
            
            if (patterns.Count == 0)
            {
                return Task.FromResult("No password patterns registered. Use register_password_pattern to add some.");
            }

            var response = $"Registered Password Patterns ({patterns.Count}):\n\n";
            foreach (var (pattern, maskedPassword) in patterns.OrderBy(x => x.Key))
            {
                response += $"• {pattern} → {maskedPassword}\n";
            }

            return Task.FromResult(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get registered passwords");
            return Task.FromResult($"Failed to get registered passwords: {ex.Message}");
        }
    }

    #endregion
}