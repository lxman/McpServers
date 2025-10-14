using System.Text.RegularExpressions;
using DocumentServer.Models.Common;
using DocumentServer.Services.Core;
using FuzzySharp;
using FuzzySharp.Extractor;

namespace DocumentServer.Services.DocumentSearch;

/// <summary>
/// Service for quick in-memory search across loaded documents
/// </summary>
public class QuickSearchService
{
    private readonly ILogger<QuickSearchService> _logger;
    private readonly DocumentProcessor _processor;

    /// <summary>
    /// Initializes a new instance of the QuickSearchService
    /// </summary>
    public QuickSearchService(ILogger<QuickSearchService> logger, DocumentProcessor processor)
    {
        _logger = logger;
        _processor = processor;
        _logger.LogInformation("QuickSearchService initialized");
    }

    /// <summary>
    /// Search for text within a specific document
    /// </summary>
    /// <param name="filePath">Full path to the document</param>
    /// <param name="searchTerm">Text to search for</param>
    /// <param name="fuzzySearch">Enable fuzzy matching (default: false)</param>
    /// <param name="maxResults">Maximum number of results to return (default: 50)</param>
    /// <param name="password">Optional password for encrypted documents</param>
    /// <returns>Service result containing search results</returns>
    public async Task<ServiceResult<SearchResult>> SearchInDocumentAsync(
        string filePath,
        string searchTerm,
        bool fuzzySearch = false,
        int maxResults = 50,
        string? password = null)
    {
        _logger.LogInformation("Searching in document: {FilePath}, Term: '{SearchTerm}', Fuzzy: {Fuzzy}",
            filePath, searchTerm, fuzzySearch);

        try
        {
            // Extract text from document
            ServiceResult<string> textResult = await _processor.ExtractTextAsync(filePath, password);
            if (!textResult.Success)
            {
                return ServiceResult<SearchResult>.CreateFailure(
                    $"Failed to extract text: {textResult.Error}");
            }

            string text = textResult.Data!;
            var matches = new List<SearchMatch>();

            if (fuzzySearch)
            {
                // Fuzzy search using FuzzySharp
                matches = PerformFuzzySearch(text, searchTerm, maxResults);
            }
            else
            {
                // Exact search using regex
                matches = PerformExactSearch(text, searchTerm, maxResults);
            }

            var searchResult = new SearchResult
            {
                FilePath = filePath,
                DocumentType = GetDocumentType(filePath),
                Matches = matches.Take(maxResults).ToList(),
                RelevanceScore = Convert.ToSingle(matches.Count > 0 ? matches.Average(m => m.FuzzyScore) / 100f : 0)
            };

            _logger.LogInformation("Search complete: {FilePath}, Found {Count} matches",
                filePath, searchResult.MatchCount);

            return ServiceResult<SearchResult>.CreateSuccess(searchResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching in document: {FilePath}", filePath);
            return ServiceResult<SearchResult>.CreateFailure(ex);
        }
    }

    /// <summary>
    /// Search across all cached documents
    /// </summary>
    /// <param name="searchTerm">Text to search for</param>
    /// <param name="fuzzySearch">Enable fuzzy matching (default: false)</param>
    /// <param name="maxResultsPerDocument">Maximum results per document (default: 10)</param>
    /// <returns>Service result containing search results from all documents</returns>
    public async Task<ServiceResult<List<SearchResult>>> SearchAcrossDocumentsAsync(
        string searchTerm,
        bool fuzzySearch = false,
        int maxResultsPerDocument = 10)
    {
        _logger.LogInformation("Searching across all cached documents: '{SearchTerm}', Fuzzy: {Fuzzy}",
            searchTerm, fuzzySearch);

        try
        {
            List<string> cachedPaths = _processor.GetCachedDocuments();
            
            if (cachedPaths.Count == 0)
            {
                _logger.LogWarning("No documents currently cached");
                return ServiceResult<List<SearchResult>>.CreateSuccess([]);
            }

            var allResults = new List<SearchResult>();
            var successCount = 0;
            var failureCount = 0;

            foreach (string filePath in cachedPaths)
            {
                ServiceResult<SearchResult> result = await SearchInDocumentAsync(
                    filePath, searchTerm, fuzzySearch, maxResultsPerDocument);

                if (result.Success && result.Data is not null)
                {
                    if (result.Data.MatchCount > 0)
                    {
                        allResults.Add(result.Data);
                    }
                    successCount++;
                }
                else
                {
                    _logger.LogWarning("Failed to search in: {FilePath}, Error: {Error}",
                        filePath, result.Error);
                    failureCount++;
                }
            }

            // Sort by relevance score
            allResults = allResults.OrderByDescending(r => r.RelevanceScore).ToList();

            int totalMatches = allResults.Sum(r => r.MatchCount);

            _logger.LogInformation("Cross-document search complete: {Total} documents, {Matches} total matches, {Success} succeeded, {Failed} failed",
                cachedPaths.Count, totalMatches, successCount, failureCount);

            return ServiceResult<List<SearchResult>>.CreateSuccess(allResults);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching across documents");
            return ServiceResult<List<SearchResult>>.CreateFailure(ex);
        }
    }

    /// <summary>
    /// Perform exact search using regex
    /// </summary>
    private List<SearchMatch> PerformExactSearch(string text, string searchTerm, int maxResults)
    {
        var matches = new List<SearchMatch>();
        
        try
        {
            var regex = new Regex(Regex.Escape(searchTerm), RegexOptions.IgnoreCase);
            MatchCollection regexMatches = regex.Matches(text);

            var count = 0;
            foreach (Match match in regexMatches)
            {
                if (count >= maxResults) break;

                matches.Add(new SearchMatch
                {
                    MatchedText = match.Value,
                    Context = GetContext(text, match.Index, match.Length),
                    Position = match.Index,
                    LineNumber = GetLineNumber(text, match.Index),
                    FuzzyScore = 100 // Exact match
                });

                count++;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error in exact search for term: '{SearchTerm}'", searchTerm);
        }

        return matches;
    }

    /// <summary>
    /// Perform fuzzy search using FuzzySharp
    /// </summary>
    private List<SearchMatch> PerformFuzzySearch(string text, string searchTerm, int maxResults)
    {
        var matches = new List<SearchMatch>();

        try
        {
            // Split text into sentences for fuzzy matching
            List<string> sentences = SplitIntoSentences(text);
            
            // Use FuzzySharp to find the best matches
            IEnumerable<ExtractedResult<string>> fuzzyMatches = Process.ExtractTop(searchTerm, sentences, limit: maxResults * 2)
                .Where(m => m.Score >= 60) // Minimum 60% similarity
                .Take(maxResults);

            foreach (ExtractedResult<string> fuzzyMatch in fuzzyMatches)
            {
                // Find the position of this sentence in the original text
                int position = text.IndexOf(fuzzyMatch.Value, StringComparison.OrdinalIgnoreCase);
                if (position >= 0)
                {
                    matches.Add(new SearchMatch
                    {
                        MatchedText = fuzzyMatch.Value.Length > 100 
                            ? fuzzyMatch.Value[..100] + "..." 
                            : fuzzyMatch.Value,
                        Context = GetContext(text, position, fuzzyMatch.Value.Length),
                        Position = position,
                        LineNumber = GetLineNumber(text, position),
                        FuzzyScore = fuzzyMatch.Score
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error in fuzzy search for term: '{SearchTerm}'", searchTerm);
        }

        return matches;
    }

    /// <summary>
    /// Get the context snippet around a match
    /// </summary>
    private string GetContext(string text, int position, int matchLength, int contextSize = 100)
    {
        int start = Math.Max(0, position - contextSize);
        int end = Math.Min(text.Length, position + matchLength + contextSize);

        string context = text[start..end];

        // Add ellipsis if truncated
        if (start > 0) context = "..." + context;
        if (end < text.Length) context = context + "...";

        // Clean up newlines for display
        context = context.Replace('\r', ' ').Replace('\n', ' ');
        
        // Collapse multiple spaces
        context = Regex.Replace(context, @"\s+", " ").Trim();

        return context;
    }

    /// <summary>
    /// Get the line number for a position in text
    /// </summary>
    private int GetLineNumber(string text, int position)
    {
        if (position < 0 || position >= text.Length) return 0;

        string textUpToPosition = text[..position];
        return textUpToPosition.Count(c => c == '\n') + 1;
    }

    /// <summary>
    /// Split text into sentences for fuzzy matching
    /// </summary>
    private List<string> SplitIntoSentences(string text)
    {
        // Simple sentence splitting on common punctuation
        string[] sentences = text.Split(['.', '!', '?', '\n'], 
            StringSplitOptions.RemoveEmptyEntries);

        return sentences
            .Select(s => s.Trim())
            .Where(s => s.Length > 10) // Ignore very short fragments
            .ToList();
    }

    /// <summary>
    /// Determine the document type from file extension
    /// </summary>
    private DocumentType GetDocumentType(string filePath)
    {
        string extension = Path.GetExtension(filePath).ToLowerInvariant();
        
        return extension switch
        {
            ".pdf" => DocumentType.Pdf,
            ".docx" or ".doc" => DocumentType.Word,
            ".xlsx" or ".xls" or ".xlsm" => DocumentType.Excel,
            ".pptx" or ".ppt" => DocumentType.PowerPoint,
            _ => DocumentType.Unknown
        };
    }
}
