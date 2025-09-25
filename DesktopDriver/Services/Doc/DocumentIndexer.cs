using System.Diagnostics;
using System.Text.RegularExpressions;
using DesktopDriver.Services.Doc.Models;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Microsoft.Extensions.Logging;
using SystemDirectory = System.IO.Directory;
// ReSharper disable InconsistentNaming

namespace DesktopDriver.Services.Doc;

public class DocumentIndexer(ILogger<DocumentIndexer> logger, DocumentProcessor documentProcessor)
    : IDisposable
{
    private readonly Dictionary<string, (FSDirectory Directory, IndexWriter Writer, Analyzer Analyzer)> _indexes = new();
    private bool _indexesDiscovered;
    
    private const LuceneVersion LUCENE_VERSION = LuceneVersion.LUCENE_48;

    public async Task<IndexingResult> BuildIndex(string indexName, string rootPath, IndexingOptions? options = null)
    {
        options ??= new IndexingOptions();
        
        var result = new IndexingResult
        {
            IndexName = indexName,
            RootPath = rootPath,
            StartTime = DateTime.UtcNow
        };

        try
        {
            logger.LogInformation("Starting indexing: {IndexName} from {RootPath}", indexName, rootPath);

            // Create or get index
            var (directory, writer, analyzer) = GetOrCreateIndex(indexName);

            // Discover documents
            var documents = DiscoverDocuments(rootPath, options);
            logger.LogInformation("Found {Count} documents to index", documents.Count);

            // Process documents with controlled concurrency
            var semaphore = new SemaphoreSlim(options.MaxConcurrency);
            var tasks = documents.Select(async doc =>
            {
                await semaphore.WaitAsync();
                try
                {
                    return await ProcessDocument(doc, writer, result, options);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            // Commit changes
            writer.Commit();
            
            result.EndTime = DateTime.UtcNow;
            logger.LogInformation("Indexing completed: {Successful} successful, {Failed} failed, Duration: {Duration}",
                result.Successful.Count, result.Failed.Count, result.Duration);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to build index: {IndexName}", indexName);
            result.EndTime = DateTime.UtcNow;
            throw;
        }
    }

    public SearchResults Search(string query, string indexName, SearchQuery? searchQuery = null)
    {
        searchQuery ??= new SearchQuery { Query = query };
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (!_indexes.TryGetValue(indexName, out var indexInfo))
            {
                throw new ArgumentException($"Index '{indexName}' not found. Available indexes: {string.Join(", ", _indexes.Keys)}");
            }

            var (luceneDir, _, analyzer) = indexInfo;

            using var reader = DirectoryReader.Open(luceneDir);
            var searcher = new IndexSearcher(reader);
            
            // Parse query
            var parser = new MultiFieldQueryParser(LUCENE_VERSION,
                ["content", "title", "filename"], analyzer);
            
            var luceneQuery = parser.Parse(searchQuery.Query);
            
            // Apply filters by creating a boolean query
            var finalQuery = ApplyFilters(luceneQuery, searchQuery);
            
            // Execute search
            var topDocs = searcher.Search(finalQuery, searchQuery.MaxResults);

            // Build results
            var results = new SearchResults
            {
                Query = query,
                TotalHits = topDocs.TotalHits,
                SearchTimeMs = stopwatch.Elapsed.TotalMilliseconds
            };

            foreach (var scoreDoc in topDocs.ScoreDocs)
            {
                var doc = searcher.Doc(scoreDoc.Doc);
                var result = new SearchResult
                {
                    FilePath = doc.Get("filepath") ?? "",
                    Title = doc.Get("title") ?? "",
                    RelevanceScore = scoreDoc.Score,
                    DocumentType = Enum.TryParse<DocumentType>(doc.Get("doctype"), out var dt) ? dt : DocumentType.Unknown,
                    ModifiedDate = DateTime.TryParse(doc.Get("modified"), out var modified) ? modified : DateTime.MinValue,
                    FileSizeBytes = long.TryParse(doc.Get("filesize"), out var size) ? size : 0
                };

                // Add snippets if requested
                if (searchQuery.IncludeSnippets)
                {
                    result.Snippets = ExtractSnippets(doc.Get("content") ?? "", searchQuery.Query, 3);
                }

                results.Results.Add(result);
                
                // Update statistics
                var docType = result.DocumentType.ToString();
                results.FileTypeCounts[docType] = results.FileTypeCounts.GetValueOrDefault(docType, 0) + 1;
                
                var dirPath = Path.GetDirectoryName(result.FilePath) ?? "";
                results.DirectoryCounts[dirPath] = results.DirectoryCounts.GetValueOrDefault(dirPath, 0) + 1;
            }

            // Sort results if needed
            if (searchQuery.SortBy != "relevance")
            {
                results.Results = SortResults(results.Results, searchQuery.SortBy, searchQuery.SortDescending);
            }

            return results;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Search failed for query: {Query} in index: {IndexName}", query, indexName);
            throw;
        }
    }

    public List<string> GetIndexNames()
    {
        // Ensure existing indexes are discovered
        EnsureIndexesDiscovered();
    
        var indexNames = new HashSet<string>(_indexes.Keys);
    
        // Add indexes that exist on disk but aren't loaded in memory
        var indexesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopDriver", "Indexes");
    
        if (SystemDirectory.Exists(indexesPath))
        {
            foreach (var indexDir in SystemDirectory.GetDirectories(indexesPath))
            {
                var indexName = Path.GetFileName(indexDir);
                try
                {
                    using var directory = FSDirectory.Open(indexDir);
                    if (DirectoryReader.IndexExists(directory))
                    {
                        indexNames.Add(indexName);
                    }
                }
                catch
                {
                    // Skip invalid directories
                }
            }
        }
    
        return indexNames.ToList();
    }
    
    public int DeduplicateIndex(string indexName)
    {
        if (!_indexes.TryGetValue(indexName, out var indexInfo))
        {
            throw new ArgumentException($"Index '{indexName}' not found or not loaded.");
        }

        var (directory, writer, analyzer) = indexInfo;
    
        // Force a merge to consolidate segments and remove duplicates
        // This will merge all segments and remove documents marked for deletion
        writer.ForceMerge(1); // Merge into a single segment
        writer.Commit();
    
        logger.LogInformation("Deduplication completed for index: {IndexName}", indexName);
    
        // Return the number of documents after deduplication
        using var reader = DirectoryReader.Open(directory);
        return reader.NumDocs;
    }
    
    public bool RemoveIndex(string indexName)
    {
        if (_indexes.TryGetValue(indexName, out var indexInfo))
        {
            var (directory, writer, analyzer) = indexInfo;
            
            writer.Dispose();
            analyzer.Dispose();
            directory.Dispose();
            
            _indexes.Remove(indexName);
            
            logger.LogInformation("Removed index: {IndexName}", indexName);
            return true;
        }
        
        return false;
    }

    private (FSDirectory Directory, IndexWriter Writer, Analyzer Analyzer) GetOrCreateIndex(string indexName)
    {
        if (_indexes.TryGetValue(indexName, out var existing))
        {
            return existing;
        }

        var indexPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopDriver", "Indexes", indexName);
    
        SystemDirectory.CreateDirectory(indexPath);
    
        var directory = FSDirectory.Open(indexPath);
        var analyzer = new StandardAnalyzer(LUCENE_VERSION);
        var config = new IndexWriterConfig(LUCENE_VERSION, analyzer);
    
        // Check if this is an existing index
        var isExistingIndex = IndexExists(directory);
    
        // If it's an existing index, open it in append mode, otherwise create new
        if (isExistingIndex)
        {
            config.OpenMode = OpenMode.APPEND; // This opens existing index for updates
            logger.LogInformation("Opening existing index: {IndexName} at {IndexPath} (reusing existing index)", indexName, indexPath);
        }
        else
        {
            config.OpenMode = OpenMode.CREATE; // This creates a new index
            logger.LogInformation("Creating new index: {IndexName} at {IndexPath}", indexName, indexPath);
        }
    
        var writer = new IndexWriter(directory, config);

        var indexInfo = (directory, writer, analyzer);
        _indexes[indexName] = indexInfo;
    
        return indexInfo;
    }

    private static bool IndexExists(FSDirectory directory)
    {
        try
        {
            return DirectoryReader.IndexExists(directory);
        }
        catch
        {
            return false;
        }
    }
    
    private void EnsureIndexesDiscovered()
    {
        if (_indexesDiscovered)
            return;
    
        var indexesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopDriver", "Indexes");
    
        if (!SystemDirectory.Exists(indexesPath))
        {
            _indexesDiscovered = true;
            return;
        }
    
        foreach (var indexDir in SystemDirectory.GetDirectories(indexesPath))
        {
            var indexName = Path.GetFileName(indexDir);
        
            try
            {
                // Check if it's a valid index
                using var testDirectory = FSDirectory.Open(indexDir);
                if (DirectoryReader.IndexExists(testDirectory))
                {
                    logger.LogInformation("Discovered existing index: {IndexName}", indexName);
                    // Index will be loaded when first accessed via GetOrCreateIndex
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Skipping invalid index directory: {IndexName}", indexName);
            }
        }
    
        _indexesDiscovered = true;
    }

    private List<FileInfo> DiscoverDocuments(string rootPath, IndexingOptions options)
    {
        var documents = new List<FileInfo>();
        
        if (!SystemDirectory.Exists(rootPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {rootPath}");
        }

        var searchOption = options.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        
        foreach (var pattern in options.IncludePatterns)
        {
            try
            {
                var files = SystemDirectory.GetFiles(rootPath, pattern, searchOption);
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    
                    // Apply size filter
                    if (fileInfo.Length > options.MaxFileSizeMB * 1024 * 1024)
                    {
                        continue;
                    }
                    
                    // Apply exclude patterns
                    var relativePath = Path.GetRelativePath(rootPath, file);
                    if (options.ExcludePatterns.Any(exclude => 
                        IsMatch(relativePath, exclude) || IsMatch(file, exclude)))
                    {
                        continue;
                    }
                    
                    documents.Add(fileInfo);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to discover files with pattern: {Pattern}", pattern);
            }
        }
        
        return documents.Distinct().ToList();
    }

    private async Task<bool> ProcessDocument(FileInfo fileInfo, IndexWriter writer, IndexingResult result, IndexingOptions options)
    {
        try
        {
            // Extract document content
            var content = await documentProcessor.ExtractContent(fileInfo.FullName);
            
            // Create Lucene document
            var doc = new Document();
            
            // Add a unique identifier field for document replacement
            // This is crucial for preventing duplicates
            var uniqueId = content.FilePath.Replace("\\", "/").ToLowerInvariant();
            doc.Add(new StringField("id", uniqueId, Field.Store.YES));
            
            // Add other fields
            doc.Add(new StringField("filepath", content.FilePath, Field.Store.YES));
            doc.Add(new TextField("filename", content.Metadata.FileName, Field.Store.YES));
            doc.Add(new TextField("title", content.Title, Field.Store.YES));
            doc.Add(new TextField("content", content.PlainText, Field.Store.YES));
            doc.Add(new StringField("doctype", content.DocumentType.ToString(), Field.Store.YES));
            doc.Add(new StringField("modified", content.Metadata.ModifiedDate.ToString("O"), Field.Store.YES));
            doc.Add(new StringField("filesize", content.Metadata.FileSizeBytes.ToString(), Field.Store.YES));
            
            // Add author and other metadata if available
            if (!string.IsNullOrWhiteSpace(content.Metadata.Author))
                doc.Add(new TextField("author", content.Metadata.Author, Field.Store.YES));
            
            if (!string.IsNullOrWhiteSpace(content.Metadata.Keywords))
                doc.Add(new TextField("keywords", content.Metadata.Keywords, Field.Store.YES));
            
            // Use UpdateDocument to replace existing document with same ID, or add new if it doesn't exist
            // This prevents duplicates by replacing documents with the same "id" field
            writer.UpdateDocument(new Term("id", uniqueId), doc);
            
            // Update statistics
            result.Successful.Add(content.FilePath);
            result.TotalSizeBytes += content.Metadata.FileSizeBytes;
            
            var docType = content.DocumentType.ToString();
            result.FileTypeStats[docType] = result.FileTypeStats.GetValueOrDefault(docType, 0) + 1;
            
            logger.LogDebug("Indexed/Updated document: {FilePath}", content.FilePath);
            
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            result.PasswordProtected.Add(new PasswordProtectedDocument
            {
                FilePath = fileInfo.FullName,
                DetectedPattern = "Password required",
                PasswordAttempted = true
            });
            
            logger.LogDebug("Password protected document: {FilePath}", fileInfo.FullName);
            
            if (!options.SkipPasswordProtected)
            {
                result.Failed.Add(new FailedDocument
                {
                    FilePath = fileInfo.FullName,
                    ErrorMessage = ex.Message
                });
            }
            
            return false;
        }
        catch (Exception ex)
        {
            result.Failed.Add(new FailedDocument
            {
                FilePath = fileInfo.FullName,
                ErrorMessage = ex.Message
            });
            
            logger.LogWarning(ex, "Failed to process document: {FilePath}", fileInfo.FullName);
            
            return false;
        }
    }

    private static bool IsMatch(string path, string pattern)
    {
        // Simple glob matching - could be enhanced with proper glob library
        if (pattern.Contains("**"))
        {
            pattern = pattern.Replace("**", "*");
        }
        
        return path.Contains(pattern.Replace("*", "")) || 
               Regex.IsMatch(path, 
                   "^" + Regex.Escape(pattern)
                       .Replace("\\*", ".*")
                       .Replace("\\?", ".") + "$",
                   RegexOptions.IgnoreCase);
    }

    private static Query ApplyFilters(Query baseQuery, SearchQuery searchQuery)
    {
        var filters = new List<Query>();

        // File type filter
        if (searchQuery.FileTypes.Count != 0)
        {
            var fileTypeQuery = new BooleanQuery();
            foreach (var fileType in searchQuery.FileTypes)
            {
                fileTypeQuery.Add(new TermQuery(new Term("doctype", fileType)), Occur.SHOULD);
            }
            filters.Add(fileTypeQuery);
        }

        // Date range filter
        if (searchQuery.StartDate.HasValue || searchQuery.EndDate.HasValue)
        {
            var startDate = searchQuery.StartDate?.ToString("O") ?? DateTime.MinValue.ToString("O");
            var endDate = searchQuery.EndDate?.ToString("O") ?? DateTime.MaxValue.ToString("O");
            
            filters.Add(TermRangeQuery.NewStringRange("modified", startDate, endDate, true, true));
        }

        if (filters.Count == 0)
            return baseQuery;

        // Combine base query with filters
        var combinedQuery = new BooleanQuery();
        combinedQuery.Add(baseQuery, Occur.MUST);

        foreach (var filter in filters)
        {
            combinedQuery.Add(filter, Occur.MUST);
        }

        return combinedQuery;
    }

    private static List<string> ExtractSnippets(string content, string query, int maxSnippets)
    {
        var snippets = new List<string>();
        var queryTerms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = content.Split('\n');
        
        foreach (var line in lines)
        {
            if (snippets.Count >= maxSnippets) break;
            
            if (queryTerms.Any(term => line.Contains(term, StringComparison.OrdinalIgnoreCase)))
            {
                var snippet = line.Trim();
                if (snippet.Length > 200)
                {
                    snippet = snippet.Substring(0, 200) + "...";
                }
                
                if (!string.IsNullOrWhiteSpace(snippet))
                {
                    snippets.Add(snippet);
                }
            }
        }
        
        return snippets;
    }

    private static List<SearchResult> SortResults(List<SearchResult> results, string sortBy, bool descending)
    {
        return sortBy.ToLower() switch
        {
            "date" => descending 
                ? results.OrderByDescending(r => r.ModifiedDate).ToList()
                : results.OrderBy(r => r.ModifiedDate).ToList(),
            "title" => descending
                ? results.OrderByDescending(r => r.Title).ToList()
                : results.OrderBy(r => r.Title).ToList(),
            "path" => descending
                ? results.OrderByDescending(r => r.FilePath).ToList()
                : results.OrderBy(r => r.FilePath).ToList(),
            "size" => descending
                ? results.OrderByDescending(r => r.FileSizeBytes).ToList()
                : results.OrderBy(r => r.FileSizeBytes).ToList(),
            _ => results // Default to relevance order
        };
    }

    public void Dispose()
    {
        foreach (var (directory, writer, analyzer) in _indexes.Values)
        {
            try
            {
                writer?.Dispose();
                analyzer?.Dispose();
                directory?.Dispose();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error disposing index resources");
            }
        }
        
        _indexes.Clear();
    }
}