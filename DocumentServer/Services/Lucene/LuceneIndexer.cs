using DocumentServer.Models.Common;
using DocumentServer.Services.Core;
using DocumentServer.Services.Lucene.Models;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Directory = System.IO.Directory;

namespace DocumentServer.Services.Lucene;

/// <summary>
/// Service for creating Lucene document indexes from directories of documents.
/// Focuses purely on the indexing process while delegating index lifecycle to IndexManager.
/// </summary>
public class LuceneIndexer
{
    private readonly ILogger<LuceneIndexer> _logger;
    private readonly DocumentProcessor _processor;
    private readonly IndexManager _indexManager;

    /// <summary>
    /// Initializes a new instance of the LuceneIndexer
    /// </summary>
    public LuceneIndexer(
        ILogger<LuceneIndexer> logger, 
        DocumentProcessor processor,
        IndexManager indexManager)
    {
        _logger = logger;
        _processor = processor;
        _indexManager = indexManager;
        
        _logger.LogInformation("LuceneIndexer initialized with IndexManager");
    }

    /// <summary>
    /// Build a Lucene index from a directory of documents
    /// </summary>
    /// <param name="indexName">Name for the index</param>
    /// <param name="rootPath">Root directory containing documents</param>
    /// <param name="includePatterns">File patterns to include (e.g., "*.pdf,*.docx")</param>
    /// <param name="recursive">Search recursively in subdirectories</param>
    /// <returns>Result of the indexing operation</returns>
    public async Task<ServiceResult<IndexingResult>> BuildIndexAsync(
        string indexName,
        string rootPath,
        string? includePatterns = null,
        bool recursive = true)
    {
        _logger.LogInformation("Building index: {IndexName} from {RootPath}", indexName, rootPath);

        var result = new IndexingResult
        {
            IndexName = indexName,
            RootPath = rootPath,
            StartTime = DateTime.UtcNow
        };

        try
        {
            if (!Directory.Exists(rootPath))
            {
                return ServiceResult<IndexingResult>.CreateFailure("Root directory does not exist");
            }

            // Check if index already exists
            if (_indexManager.IndexExists(indexName))
            {
                _logger.LogWarning("Index {IndexName} already exists. Will append to existing index.", indexName);
            }
            else
            {
                _logger.LogInformation("Creating new index: {IndexName}", indexName);
                CreateNewIndex(indexName);
            }

            // Get index resources from IndexManager
            IndexResources indexResources = _indexManager.GetIndexResources(indexName);

            // Discover documents
            List<FileInfo> documents = DiscoverDocuments(rootPath, includePatterns, recursive);
            _logger.LogInformation("Found {Count} documents to index", documents.Count);

            result.TotalDocuments = documents.Count;

            // Process documents
            foreach (FileInfo fileInfo in documents)
            {
                bool success = await ProcessDocumentAsync(fileInfo, indexResources.Writer, result);
                if (success)
                {
                    result.IndexedDocuments++;
                }
            }

            // Commit changes
            indexResources.Writer.Commit();

            result.EndTime = DateTime.UtcNow;

            _logger.LogInformation("Indexing complete: {Indexed}/{Total} documents, Duration: {Duration}",
                result.IndexedDocuments, result.TotalDocuments, result.Duration);

            return ServiceResult<IndexingResult>.CreateSuccess(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build index: {IndexName}", indexName);
            result.EndTime = DateTime.UtcNow;
            return ServiceResult<IndexingResult>.CreateFailure(ex);
        }
    }

    #region Private Methods

    /// <summary>
    /// Creates a new index by ensuring the directory exists and registering with IndexManager.
    /// </summary>
    private void CreateNewIndex(string indexName)
    {
        string indexPath = Path.Combine(_indexManager.GetIndexBasePath(), indexName);
        Directory.CreateDirectory(indexPath);
        
        // Register with IndexManager so it knows about the new index
        _indexManager.RegisterIndex(indexName);
        
        _logger.LogDebug("Created new index directory: {IndexPath}", indexPath);
    }

    /// <summary>
    /// Discovers documents in the root path matching the specified patterns.
    /// </summary>
    private List<FileInfo> DiscoverDocuments(string rootPath, string? includePatterns, bool recursive)
    {
        var documents = new List<FileInfo>();
        SearchOption searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        // Parse include patterns
        List<string> patterns = string.IsNullOrWhiteSpace(includePatterns)
            ? _processor.GetSupportedExtensions()
            : includePatterns.Split(',').Select(p => p.Trim()).ToList();

        foreach (string pattern in patterns)
        {
            try
            {
                string searchPattern = pattern.StartsWith("*") ? pattern : $"*{pattern}";
                documents.AddRange(Directory.GetFiles(rootPath, searchPattern, searchOption)
                    .Select(f => new FileInfo(f)));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error searching for pattern: {Pattern}", pattern);
            }
        }

        return documents.Distinct().ToList();
    }

    /// <summary>
    /// Processes a single document and adds it to the index.
    /// </summary>
    private async Task<bool> ProcessDocumentAsync(FileInfo fileInfo, IndexWriter writer, IndexingResult result)
    {
        try
        {
            _logger.LogDebug("Indexing document: {FilePath}", fileInfo.FullName);

            // Extract content
            ServiceResult<string> textResult = await _processor.ExtractTextAsync(fileInfo.FullName);
            if (!textResult.Success)
            {
                result.FailedDocuments++;
                _logger.LogWarning("Failed to extract text from: {FilePath}, Error: {Error}",
                    fileInfo.FullName, textResult.Error);
                return false;
            }

            // Create Lucene document
            var doc = new Document
            {
                new TextField("content", textResult.Data!, Field.Store.YES),
                new StringField("filepath", fileInfo.FullName, Field.Store.YES),
                new StringField("filename", fileInfo.Name, Field.Store.YES),
                new StringField("title", Path.GetFileNameWithoutExtension(fileInfo.Name), Field.Store.YES),
                new StringField("doctype", Path.GetExtension(fileInfo.Name).TrimStart('.'), Field.Store.YES),
                new StringField("modified", fileInfo.LastWriteTime.ToString("o"), Field.Store.YES),
                new StringField("filesize", fileInfo.Length.ToString(), Field.Store.YES)
            };

            writer.AddDocument(doc);

            return true;
        }
        catch (Exception ex)
        {
            result.FailedDocuments++;
            _logger.LogError(ex, "Error indexing document: {FilePath}", fileInfo.FullName);
            return false;
        }
    }

    #endregion
}
