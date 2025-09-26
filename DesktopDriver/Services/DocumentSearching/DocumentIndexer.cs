using System.Diagnostics;
using System.Text.RegularExpressions;
using DesktopDriver.Services.DocumentSearching.Models;
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

namespace DesktopDriver.Services.DocumentSearching;

public class DocumentIndexer : IDisposable
{
    private readonly ILogger<DocumentIndexer> _logger;
    private readonly DocumentProcessor _documentProcessor;
    private readonly Dictionary<string, (FSDirectory Directory, IndexWriter Writer, Analyzer Analyzer)> _indexes = new();
    private readonly HashSet<string> _discoveredIndexNames = [];
    
    private const LuceneVersion LUCENE_VERSION = LuceneVersion.LUCENE_48;

    public DocumentIndexer(ILogger<DocumentIndexer> logger, DocumentProcessor documentProcessor)
    {
        _logger = logger;
        _documentProcessor = documentProcessor;
        
        DiscoverExistingIndexes();
    }

    private void DiscoverExistingIndexes()
    {
        try
        {
            string indexesBasePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DesktopDriver", "Indexes");

            if (!SystemDirectory.Exists(indexesBasePath))
            {
                _logger.LogDebug("Indexes directory does not exist: {IndexesPath}", indexesBasePath);
                return;
            }

            string[] indexDirectories = SystemDirectory.GetDirectories(indexesBasePath);
            foreach (string indexDir in indexDirectories)
            {
                string indexName = Path.GetFileName(indexDir);
                
                // Verify it looks like a Lucene index (has segments files)
                bool hasSegments = SystemDirectory.GetFiles(indexDir, "segments*").Any();
                if (hasSegments)
                {
                    _discoveredIndexNames.Add(indexName);
                    _logger.LogDebug("Discovered existing index: {IndexName}", indexName);
                }
            }
            
            if (_discoveredIndexNames.Count != 0)
            {
                _logger.LogInformation("Discovered {Count} existing indexes: {IndexNames}", 
                    _discoveredIndexNames.Count, string.Join(", ", _discoveredIndexNames.OrderBy(x => x)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to discover existing indexes");
        }
    }

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
            _logger.LogInformation("Starting indexing: {IndexName} from {RootPath}", indexName, rootPath);

            // Create or get an index
            (FSDirectory directory, IndexWriter writer, Analyzer analyzer) = GetOrCreateIndex(indexName);

            // Discover documents
            List<FileInfo> documents = DiscoverDocuments(rootPath, options);
            _logger.LogInformation("Found {Count} documents to index", documents.Count);

            // Process documents with controlled concurrency
            var semaphore = new SemaphoreSlim(options.MaxConcurrency);
            IEnumerable<Task<bool>> tasks = documents.Select(async doc =>
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
            _logger.LogInformation("Indexing completed: {Successful} successful, {Failed} failed, Duration: {Duration}",
                result.Successful.Count, result.Failed.Count, result.Duration);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build index: {IndexName}", indexName);
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
            // MODIFIED: Check discovered indexes first, then lazy load if needed
            if (!_discoveredIndexNames.Contains(indexName))
            {
                throw new ArgumentException($"Index '{indexName}' not found. Available indexes: {string.Join(", ", _discoveredIndexNames.OrderBy(x => x))}");
            }

            // Lazy load the index resources if not already loaded
            if (!_indexes.TryGetValue(indexName, out (FSDirectory Directory, IndexWriter Writer, Analyzer Analyzer) indexInfo))
            {
                indexInfo = GetOrCreateIndex(indexName);
            }

            (FSDirectory luceneDir, _, Analyzer analyzer) = indexInfo;

            using DirectoryReader? reader = DirectoryReader.Open(luceneDir);
            var searcher = new IndexSearcher(reader);
            
            // Parse query
            var parser = new MultiFieldQueryParser(LUCENE_VERSION,
                ["content", "title", "filename"], analyzer);
            
            Query? luceneQuery = parser.Parse(searchQuery.Query);
            
            // Apply filters by creating a boolean query
            Query finalQuery = ApplyFilters(luceneQuery, searchQuery);
            
            // Execute search
            TopDocs topDocs = searcher.Search(finalQuery, searchQuery.MaxResults);

            // Build results
            var results = new SearchResults
            {
                Query = query,
                TotalHits = topDocs.TotalHits,
                SearchTimeMs = stopwatch.Elapsed.TotalMilliseconds
            };

            foreach (ScoreDoc? scoreDoc in topDocs.ScoreDocs)
            {
                Document doc = searcher.Doc(scoreDoc.Doc);
                var result = new SearchResult
                {
                    FilePath = doc.Get("filepath") ?? "",
                    Title = doc.Get("title") ?? "",
                    RelevanceScore = scoreDoc.Score,
                    DocumentType = Enum.TryParse<DocumentType>(doc.Get("doctype"), out DocumentType dt) ? dt : DocumentType.Unknown,
                    ModifiedDate = DateTime.TryParse(doc.Get("modified"), out DateTime modified) ? modified : DateTime.MinValue,
                    FileSizeBytes = long.TryParse(doc.Get("filesize"), out long size) ? size : 0
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
                
                string dirPath = Path.GetDirectoryName(result.FilePath) ?? "";
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
            _logger.LogError(ex, "Search failed for query: {Query} in index: {IndexName}", query, indexName);
            throw;
        }
    }

    public Task<bool> IndexExists(string indexName)
    {
        // MODIFIED: Check discovered indexes instead of just loaded ones
        return Task.FromResult(_discoveredIndexNames.Contains(indexName));
    }

    public Task<List<string>> GetIndexNames()
    {
        // MODIFIED: Return discovered indexes instead of just loaded ones
        return Task.FromResult(_discoveredIndexNames.OrderBy(x => x).ToList());
    }

    /// <summary>
    /// Unloads an index from memory while keeping it discoverable.
    /// The index can be lazy-loaded again when needed.
    /// </summary>
    /// <param name="indexName">Name of the index to unload from memory</param>
    /// <returns>True if index was unloaded, false if it wasn't loaded</returns>
    public bool UnloadIndex(string indexName)
    {
        if (_indexes.TryGetValue(indexName, out (FSDirectory Directory, IndexWriter Writer, Analyzer Analyzer) indexInfo))
        {
            (FSDirectory directory, IndexWriter writer, Analyzer analyzer) = indexInfo;
            
            try
            {
                writer.Dispose();
                analyzer.Dispose();
                directory.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing index resources for: {IndexName}", indexName);
            }
            
            _indexes.Remove(indexName);
            
            // Keep in discovered indexes - this is the key difference from RemoveIndex
            // _discoveredIndexNames still contains indexName
            
            _logger.LogInformation("Unloaded index from memory: {IndexName}", indexName);
            return true;
        }
        
        _logger.LogDebug("Index not loaded in memory: {IndexName}", indexName);
        return false;
    }

    /// <summary>
    /// Gets the memory status of all indexes
    /// </summary>
    /// <returns>Dictionary with index names and their memory load status</returns>
    public Task<Dictionary<string, IndexMemoryStatus>> GetIndexMemoryStatus()
    {
        var status = new Dictionary<string, IndexMemoryStatus>();
        
        foreach (string indexName in _discoveredIndexNames)
        {
            status[indexName] = new IndexMemoryStatus
            {
                IndexName = indexName,
                IsDiscovered = true,
                IsLoadedInMemory = _indexes.ContainsKey(indexName),
                EstimatedMemoryUsageMb = _indexes.ContainsKey(indexName) ? GetEstimatedMemoryUsage(indexName) : 0
            };
        }
        
        return Task.FromResult(status);
    }

    /// <summary>
    /// Unloads all indexes from memory while keeping them discoverable
    /// </summary>
    /// <returns>Number of indexes unloaded</returns>
    public int UnloadAllIndexes()
    {
        List<string> indexesToUnload = _indexes.Keys.ToList();
        var unloadedCount = 0;
        
        foreach (string indexName in indexesToUnload)
        {
            if (UnloadIndex(indexName))
            {
                unloadedCount++;
            }
        }
        
        _logger.LogInformation("Unloaded {Count} indexes from memory", unloadedCount);
        return unloadedCount;
    }

    /// <summary>
    /// Completely removes an index from the system (both memory and discovery).
    /// Use UnloadIndex() if you only want to free memory while keeping the index discoverable.
    /// </summary>
    /// <param name="indexName">Name of the index to completely remove</param>
    /// <returns>True if index was removed</returns>
    public bool RemoveIndex(string indexName)
    {
        // First unload from memory if loaded
        UnloadIndex(indexName);
        
        // Then remove from discovery (this is what makes it "completely removed")
        _discoveredIndexNames.Remove(indexName);
        
        // Optionally delete the index files from disk
        // (current implementation doesn't do this - should it?)
        
        _logger.LogInformation("Completely removed index: {IndexName}", indexName);
        return true;
    }

    private double GetEstimatedMemoryUsage(string indexName)
    {
        // Simple estimation - in a real implementation you might want to
        // track actual memory usage or calculate based on index size
        if (_indexes.ContainsKey(indexName))
        {
            try
            {
                string indexPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "DesktopDriver", "Indexes", indexName);
                
                if (SystemDirectory.Exists(indexPath))
                {
                    string[] indexFiles = SystemDirectory.GetFiles(indexPath);
                    long totalBytes = indexFiles.Sum(f => new FileInfo(f).Length);
                    return totalBytes / (1024.0 * 1024.0); // Convert to MB
                }
            }
            catch
            {
                // Ignore errors, return default estimate
            }
            
            return 50.0; // Default estimate if we can't calculate
        }
        
        return 0.0;
    }

    private (FSDirectory Directory, IndexWriter Writer, Analyzer Analyzer) GetOrCreateIndex(string indexName)
    {
        if (_indexes.TryGetValue(indexName, out (FSDirectory Directory, IndexWriter Writer, Analyzer Analyzer) existing))
        {
            return existing;
        }

        string indexPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopDriver", "Indexes", indexName);
        
        SystemDirectory.CreateDirectory(indexPath);
        
        FSDirectory? directory = FSDirectory.Open(indexPath);
        var analyzer = new StandardAnalyzer(LUCENE_VERSION);
        var config = new IndexWriterConfig(LUCENE_VERSION, analyzer);
        var writer = new IndexWriter(directory, config);

        (FSDirectory directory, IndexWriter writer, StandardAnalyzer analyzer) indexInfo = (directory, writer, analyzer);
        _indexes[indexName] = indexInfo;
        
        // MODIFIED: Add to discovered indexes when creating new ones
        _discoveredIndexNames.Add(indexName);
        
        _logger.LogInformation("Created/opened index: {IndexName} at {IndexPath}", indexName, indexPath);
        
        return indexInfo;
    }

    private List<FileInfo> DiscoverDocuments(string rootPath, IndexingOptions options)
    {
        var documents = new List<FileInfo>();
        
        if (!SystemDirectory.Exists(rootPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {rootPath}");
        }

        SearchOption searchOption = options.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        
        foreach (string pattern in options.IncludePatterns)
        {
            try
            {
                string[] files = SystemDirectory.GetFiles(rootPath, pattern, searchOption);
                foreach (string file in files)
                {
                    var fileInfo = new FileInfo(file);
                    
                    // Apply size filter
                    if (fileInfo.Length > options.MaxFileSizeMB * 1024 * 1024)
                    {
                        continue;
                    }
                    
                    // Apply exclude patterns
                    string relativePath = Path.GetRelativePath(rootPath, file);
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
                _logger.LogWarning(ex, "Failed to discover files with pattern: {Pattern}", pattern);
            }
        }
        
        return documents.Distinct().ToList();
    }

    private async Task<bool> ProcessDocument(FileInfo fileInfo, IndexWriter writer, IndexingResult result, IndexingOptions options)
    {
        try
        {
            // Extract document content
            DocumentContent content = await _documentProcessor.ExtractContent(fileInfo.FullName);
            
            // Create Lucene document
            Document doc =
            [
                new StringField("filepath", content.FilePath, Field.Store.YES),
                new TextField("filename", content.Metadata.FileName, Field.Store.YES),
                new TextField("title", content.Title, Field.Store.YES),
                new TextField("content", content.PlainText, Field.Store.YES),
                new StringField("doctype", content.DocumentType.ToString(), Field.Store.YES),
                new StringField("modified", content.Metadata.ModifiedDate.ToString("O"), Field.Store.YES),
                new StringField("filesize", content.Metadata.FileSizeBytes.ToString(), Field.Store.YES)

                // Add author and other metadata if available
            ];
            
            // Add fields

            // Add author and other metadata if available
            if (!string.IsNullOrWhiteSpace(content.Metadata.Author))
                doc.Add(new TextField("author", content.Metadata.Author, Field.Store.YES));
            
            if (!string.IsNullOrWhiteSpace(content.Metadata.Keywords))
                doc.Add(new TextField("keywords", content.Metadata.Keywords, Field.Store.YES));
            
            // Add to index
            writer.AddDocument(doc);
            
            // Update statistics
            result.Successful.Add(content.FilePath);
            result.TotalSizeBytes += content.Metadata.FileSizeBytes;
            
            var docType = content.DocumentType.ToString();
            result.FileTypeStats[docType] = result.FileTypeStats.GetValueOrDefault(docType, 0) + 1;
            
            _logger.LogDebug("Indexed document: {FilePath}", content.FilePath);
            
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
            
            _logger.LogDebug("Password protected document: {FilePath}", fileInfo.FullName);
            
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
            
            _logger.LogWarning(ex, "Failed to process document: {FilePath}", fileInfo.FullName);
            
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
            foreach (string fileType in searchQuery.FileTypes)
            {
                fileTypeQuery.Add(new TermQuery(new Term("doctype", fileType)), Occur.SHOULD);
            }
            filters.Add(fileTypeQuery);
        }

        // Date range filter
        if (searchQuery.StartDate.HasValue || searchQuery.EndDate.HasValue)
        {
            string startDate = searchQuery.StartDate?.ToString("O") ?? DateTime.MinValue.ToString("O");
            string endDate = searchQuery.EndDate?.ToString("O") ?? DateTime.MaxValue.ToString("O");
            
            filters.Add(TermRangeQuery.NewStringRange("modified", startDate, endDate, true, true));
        }

        if (filters.Count == 0)
            return baseQuery;

        // Combine base query with filters
        var combinedQuery = new BooleanQuery();
        combinedQuery.Add(baseQuery, Occur.MUST);

        foreach (Query filter in filters)
        {
            combinedQuery.Add(filter, Occur.MUST);
        }

        return combinedQuery;
    }

    private static List<string> ExtractSnippets(string content, string query, int maxSnippets)
    {
        var snippets = new List<string>();
        string[] queryTerms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string[] lines = content.Split('\n');
        
        foreach (string line in lines)
        {
            if (snippets.Count >= maxSnippets) break;
            
            if (queryTerms.Any(term => line.Contains(term, StringComparison.OrdinalIgnoreCase)))
            {
                string snippet = line.Trim();
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
                _logger.LogWarning(ex, "Error disposing index resources");
            }
        }
        
        _indexes.Clear();
    }
}