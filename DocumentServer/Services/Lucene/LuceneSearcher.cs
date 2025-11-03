using System.Diagnostics;
using DocumentServer.Models.Common;
using DocumentServer.Services.Lucene.Models;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Util;

namespace DocumentServer.Services.Lucene;

/// <summary>
/// Provides search functionality for Lucene indexes.
/// Handles query parsing, filtering, snippet extraction, and result sorting.
/// </summary>
public class LuceneSearcher(ILogger<LuceneSearcher> logger, IndexManager indexManager)
{
    private const LuceneVersion LUCENE_VERSION = LuceneVersion.LUCENE_48;

    /// <summary>
    /// Searches an index with the specified query and options.
    /// </summary>
    /// <param name="query">The search query string</param>
    /// <param name="indexName">Name of the index to search</param>
    /// <param name="options">Optional search configuration</param>
    /// <returns>Search results with matched documents</returns>
    public LuceneSearchResults Search(string query, string indexName, SearchOptions? options = null)
    {
        options ??= new SearchOptions { Query = query };
        var stopwatch = Stopwatch.StartNew();

        try
        {
            logger.LogInformation("Starting search in index: {IndexName}, query: {Query}", indexName, query);

            // Verify index exists
            if (!indexManager.IndexExists(indexName))
            {
                string availableIndexes = string.Join(", ", indexManager.GetIndexNames().OrderBy(x => x));
                throw new ArgumentException(
                    $"Index '{indexName}' not found. Available indexes: {availableIndexes}");
            }

            // Get index resources (lazy load if needed)
            IndexResources indexResources = indexManager.GetIndexResources(indexName);

            using DirectoryReader? reader = DirectoryReader.Open(indexResources.Directory);
            var searcher = new IndexSearcher(reader);

            // Parse query
            var parser = new MultiFieldQueryParser(LUCENE_VERSION,
                ["content", "title", "filename"], indexResources.Analyzer);

            Query? luceneQuery = parser.Parse(options.Query);

            // Apply filters
            Query finalQuery = ApplyFilters(luceneQuery, options);

            // Execute search
            TopDocs topDocs = searcher.Search(finalQuery, options.MaxResults);

            logger.LogInformation("Search found {TotalHits} hits in {ElapsedMs}ms",
                topDocs.TotalHits, stopwatch.Elapsed.TotalMilliseconds);

            // Build results
            var results = new LuceneSearchResults
            {
                Query = query,
                TotalHits = topDocs.TotalHits,
                SearchTimeMs = stopwatch.Elapsed.TotalMilliseconds
            };

            foreach (ScoreDoc? scoreDoc in topDocs.ScoreDocs)
            {
                Document doc = searcher.Doc(scoreDoc.Doc);
                var result = new LuceneSearchResult
                {
                    FilePath = doc.Get("filepath") ?? "",
                    Title = doc.Get("title") ?? "",
                    RelevanceScore = scoreDoc.Score,
                    DocumentType = Enum.TryParse(doc.Get("doctype"), out DocumentType dt)
                        ? dt.ToString()
                        : nameof(DocumentType.Unknown),
                    ModifiedDate = DateTime.TryParse(doc.Get("modified"), out DateTime modified)
                        ? modified
                        : DateTime.MinValue,
                    FileSizeBytes = long.TryParse(doc.Get("filesize"), out long size) ? size : 0
                };

                // Add snippets if requested
                if (options.IncludeSnippets)
                {
                    result.Snippets = ExtractSnippets(doc.Get("content") ?? "", options.Query, 3);
                }

                results.Results.Add(result);

                // Update statistics
                string docType = result.DocumentType;
                results.FileTypeCounts[docType] = results.FileTypeCounts.GetValueOrDefault(docType, 0) + 1;

                string dirPath = Path.GetDirectoryName(result.FilePath) ?? "";
                results.DirectoryCounts[dirPath] = results.DirectoryCounts.GetValueOrDefault(dirPath, 0) + 1;
            }

            // Sort results if needed
            if (options.SortBy != "relevance")
            {
                results.Results = SortResults(results.Results, options.SortBy, options.SortDescending);
            }

            logger.LogInformation("Search completed successfully, returning {Count} results",
                results.Results.Count);

            return results;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Search failed for query: {Query} in index: {IndexName}", query, indexName);
            throw;
        }
    }

    /// <summary>
    /// Tests a query against an index without returning full results.
    /// Useful for validating queries and estimating result counts.
    /// </summary>
    public int TestQuery(string query, string indexName)
    {
        try
        {
            logger.LogInformation("Testing query in index: {IndexName}, query: {Query}", indexName, query);

            if (!indexManager.IndexExists(indexName))
            {
                throw new ArgumentException($"Index '{indexName}' not found");
            }

            IndexResources indexResources = indexManager.GetIndexResources(indexName);

            using DirectoryReader? reader = DirectoryReader.Open(indexResources.Directory);
            var searcher = new IndexSearcher(reader);

            var parser = new MultiFieldQueryParser(LUCENE_VERSION,
                ["content", "title", "filename"], indexResources.Analyzer);

            Query? luceneQuery = parser.Parse(query);
            TopDocs topDocs = searcher.Search(luceneQuery, 1); // Just get count

            logger.LogInformation("Test query found {TotalHits} hits", topDocs.TotalHits);
            return topDocs.TotalHits;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Test query failed: {Query} in index: {IndexName}", query, indexName);
            throw;
        }
    }

    /// <summary>
    /// Applies filters to the base query based on search options.
    /// </summary>
    private static Query ApplyFilters(Query baseQuery, SearchOptions options)
    {
        var filters = new List<Query>();

        // File type filter
        if (options.FileTypes.Count != 0)
        {
            var fileTypeQuery = new BooleanQuery();
            foreach (string fileType in options.FileTypes)
            {
                fileTypeQuery.Add(new TermQuery(new Term("doctype", fileType)), Occur.SHOULD);
            }
            filters.Add(fileTypeQuery);
        }

        // Date range filter
        if (options.StartDate.HasValue || options.EndDate.HasValue)
        {
            string startDate = options.StartDate?.ToString("O") ?? DateTime.MinValue.ToString("O");
            string endDate = options.EndDate?.ToString("O") ?? DateTime.MaxValue.ToString("O");

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

    /// <summary>
    /// Extracts relevant snippets from content that match the query terms.
    /// </summary>
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

    /// <summary>
    /// Sorts search results based on specified criteria.
    /// </summary>
    private static List<LuceneSearchResult> SortResults(
        List<LuceneSearchResult> results,
        string sortBy,
        bool descending)
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
}

/// <summary>
/// Configuration options for Lucene searches.
/// </summary>
public class SearchOptions
{
    public string Query { get; set; } = string.Empty;
    public int MaxResults { get; set; } = 50;
    public bool IncludeSnippets { get; set; } = true;
    public string SortBy { get; set; } = "relevance"; // relevance, date, title, path, size
    public bool SortDescending { get; set; } = true;
    public List<string> FileTypes { get; set; } = [];
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
