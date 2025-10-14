using DocumentServer.Models.Common;
using DocumentServer.Services.Analysis.Models;
using DocumentServer.Services.Core;

namespace DocumentServer.Services.Analysis;

/// <summary>
/// Service for comparing documents
/// </summary>
public class DocumentComparator
{
    private readonly ILogger<DocumentComparator> _logger;
    private readonly DocumentProcessor _processor;

    /// <summary>
    /// Initializes a new instance of the DocumentComparator
    /// </summary>
    public DocumentComparator(ILogger<DocumentComparator> logger, DocumentProcessor processor)
    {
        _logger = logger;
        _processor = processor;
        _logger.LogInformation("DocumentComparator initialized");
    }

    /// <summary>
    /// Compare two documents for content similarity
    /// </summary>
    /// <param name="filePath1">Path to the first document</param>
    /// <param name="filePath2">Path to the second document</param>
    /// <param name="password1">Optional password for first document</param>
    /// <param name="password2">Optional password for second document</param>
    /// <returns>Service result containing comparison results</returns>
    public async Task<ServiceResult<ComparisonResult>> CompareAsync(
        string filePath1, 
        string filePath2, 
        string? password1 = null, 
        string? password2 = null)
    {
        _logger.LogInformation("Comparing documents: {File1} vs {File2}", filePath1, filePath2);

        try
        {
            // Extract text from both documents
            ServiceResult<string> text1Result = await _processor.ExtractTextAsync(filePath1, password1);
            if (!text1Result.Success)
            {
                return ServiceResult<ComparisonResult>.CreateFailure(
                    $"Failed to extract text from first document: {text1Result.Error}");
            }

            ServiceResult<string> text2Result = await _processor.ExtractTextAsync(filePath2, password2);
            if (!text2Result.Success)
            {
                return ServiceResult<ComparisonResult>.CreateFailure(
                    $"Failed to extract text from second document: {text2Result.Error}");
            }

            string text1 = text1Result.Data!;
            string text2 = text2Result.Data!;

            // Calculate similarity metrics
            var comparisonResult = new ComparisonResult
            {
                FilePath1 = filePath1,
                FilePath2 = filePath2,
                Document1Length = text1.Length,
                Document2Length = text2.Length
            };

            // Character-level comparison
            comparisonResult.CharacterSimilarity = CalculateLevenshteinSimilarity(text1, text2);

            // Word-level comparison
            string[] words1 = text1.Split([' ', '\n', '\r', '\t'], 
                StringSplitOptions.RemoveEmptyEntries);
            string[] words2 = text2.Split([' ', '\n', '\r', '\t'], 
                StringSplitOptions.RemoveEmptyEntries);

            comparisonResult.Document1WordCount = words1.Length;
            comparisonResult.Document2WordCount = words2.Length;

            // Calculate word overlap
            var uniqueWords1 = new HashSet<string>(words1, StringComparer.OrdinalIgnoreCase);
            var uniqueWords2 = new HashSet<string>(words2, StringComparer.OrdinalIgnoreCase);
            var commonWords = new HashSet<string>(uniqueWords1, StringComparer.OrdinalIgnoreCase);
            commonWords.IntersectWith(uniqueWords2);

            comparisonResult.CommonWords = commonWords.Count;
            comparisonResult.WordOverlapPercentage = uniqueWords1.Count > 0 
                ? (commonWords.Count * 100.0) / uniqueWords1.Count 
                : 0;

            // Overall similarity score (weighted average)
            comparisonResult.OverallSimilarity = 
                (comparisonResult.CharacterSimilarity * 0.3 + 
                 comparisonResult.WordOverlapPercentage * 0.7) / 100.0;

            // Determine if documents are similar
            comparisonResult.AreSimilar = comparisonResult.OverallSimilarity > 0.7;

            // Check if documents are identical
            comparisonResult.AreIdentical = comparisonResult.OverallSimilarity >= 0.99;

            // Generate summary
            comparisonResult.Summary = comparisonResult.AreIdentical
                ? "Documents are identical"
                : comparisonResult.AreSimilar
                    ? $"Documents are similar ({comparisonResult.SimilarityScore:F1}% match)"
                    : $"Documents are different ({comparisonResult.SimilarityScore:F1}% match)";

            // List major differences
            if (!comparisonResult.AreIdentical)
            {
                if (Math.Abs(text1.Length - text2.Length) > 100)
                {
                    comparisonResult.Differences.Add(
                        $"Length difference: {Math.Abs(text1.Length - text2.Length)} characters");
                }

                if (Math.Abs(comparisonResult.Document1WordCount - comparisonResult.Document2WordCount) > 10)
                {
                    comparisonResult.Differences.Add(
                        $"Word count difference: {Math.Abs(comparisonResult.Document1WordCount - comparisonResult.Document2WordCount)} words");
                }

                if (comparisonResult.WordOverlapPercentage < 50)
                {
                    comparisonResult.Differences.Add(
                        $"Low word overlap: only {comparisonResult.WordOverlapPercentage:F1}% common words");
                }
            }


            _logger.LogInformation("Comparison complete: {File1} vs {File2}, Similarity={Similarity:P}",
                filePath1, filePath2, comparisonResult.OverallSimilarity);

            return ServiceResult<ComparisonResult>.CreateSuccess(comparisonResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error comparing documents: {File1} vs {File2}", filePath1, filePath2);
            return ServiceResult<ComparisonResult>.CreateFailure(ex);
        }
    }

    /// <summary>
    /// Calculate Levenshtein distance-based similarity (0-100%)
    /// </summary>
    private double CalculateLevenshteinSimilarity(string str1, string str2)
    {
        // For very long strings, sample them to avoid performance issues
        const int maxLength = 10000;
        if (str1.Length > maxLength) str1 = str1[..maxLength];
        if (str2.Length > maxLength) str2 = str2[..maxLength];

        int distance = LevenshteinDistance(str1, str2);
        int maxLengthActual = Math.Max(str1.Length, str2.Length);
        
        if (maxLengthActual == 0) return 100.0;
        
        double similarity = (1.0 - (double)distance / maxLengthActual) * 100.0;
        return Math.Max(0, Math.Min(100, similarity));
    }

    /// <summary>
    /// Calculate Levenshtein distance between two strings
    /// </summary>
    private int LevenshteinDistance(string str1, string str2)
    {
        var matrix = new int[str1.Length + 1, str2.Length + 1];

        for (var i = 0; i <= str1.Length; i++)
            matrix[i, 0] = i;

        for (var j = 0; j <= str2.Length; j++)
            matrix[0, j] = j;

        for (var i = 1; i <= str1.Length; i++)
        {
            for (var j = 1; j <= str2.Length; j++)
            {
                int cost = str1[i - 1] == str2[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[str1.Length, str2.Length];
    }
}