using DocumentServer.Models.Common;
using DocumentServer.Services.Analysis.Models;
using DocumentServer.Services.Core;

namespace DocumentServer.Services.Analysis;

/// <summary>
/// Service for analyzing document structure and content statistics
/// </summary>
public class StructureAnalyzer
{
    private readonly ILogger<StructureAnalyzer> _logger;
    private readonly DocumentProcessor _processor;

    /// <summary>
    /// Initializes a new instance of the StructureAnalyzer
    /// </summary>
    public StructureAnalyzer(ILogger<StructureAnalyzer> logger, DocumentProcessor processor)
    {
        _logger = logger;
        _processor = processor;
        _logger.LogInformation("StructureAnalyzer initialized");
    }

    /// <summary>
    /// Analyze the structure and content of a document
    /// </summary>
    /// <param name="filePath">Full path to the document</param>
    /// <param name="password">Optional password for encrypted documents</param>
    /// <returns>Service result containing analysis results</returns>
    public async Task<ServiceResult<StructureAnalysis>> AnalyzeAsync(string filePath, string? password = null)
    {
        _logger.LogInformation("Analyzing document structure: {FilePath}", filePath);

        try
        {
            var analysis = new StructureAnalysis
            {
                FilePath = filePath
            };

            // Get document info
            ServiceResult<DocumentInfo> infoResult = await _processor.GetDocumentInfoAsync(filePath, password);
            if (infoResult.Success)
            {
                analysis.DocumentType = infoResult.Data!.DocumentType.ToString();
                analysis.FileSizeBytes = infoResult.Data.SizeBytes;
                analysis.PageCount = infoResult.Data.PageCount;
            }

            // Extract text for content analysis
            ServiceResult<string> textResult = await _processor.ExtractTextAsync(filePath, password);
            if (!textResult.Success)
            {
                _logger.LogWarning("Failed to extract text for analysis: {FilePath}, Error: {Error}",
                    filePath, textResult.Error);
                return ServiceResult<StructureAnalysis>.CreateFailure(
                    $"Failed to extract content: {textResult.Error}");
            }

            string text = textResult.Data!;
            AnalyzeTextContent(text, analysis);

            // Get structured content
            ServiceResult<object> structuredResult = 
                await _processor.ExtractStructuredContentAsync(filePath, password);
            if (structuredResult.Success)
            {
                analysis.StructuredContent = structuredResult.Data;
            }

            // Get metadata
            ServiceResult<Dictionary<string, string>> metadataResult = 
                await _processor.ExtractMetadataAsync(filePath, password);
            if (metadataResult.Success)
            {
                analysis.Metadata = metadataResult.Data;
            }

            _logger.LogInformation("Structure analysis complete: {FilePath}, Words={WordCount}, Lines={LineCount}",
                filePath, analysis.WordCount, analysis.LineCount);

            return ServiceResult<StructureAnalysis>.CreateSuccess(analysis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing document structure: {FilePath}", filePath);
            return ServiceResult<StructureAnalysis>.CreateFailure(ex);
        }
    }

    /// <summary>
    /// Analyze text content and populate statistics
    /// </summary>
    private void AnalyzeTextContent(string text, StructureAnalysis analysis)
    {
        analysis.CharacterCount = text.Length;
        analysis.CharacterCountNoSpaces = text.Count(c => !char.IsWhiteSpace(c));

        // Line analysis
        string[] lines = text.Split('\n');
        analysis.LineCount = lines.Length;
        analysis.EmptyLineCount = lines.Count(string.IsNullOrWhiteSpace);

        // Word analysis
        string[] words = text.Split([' ', '\n', '\r', '\t'], 
            StringSplitOptions.RemoveEmptyEntries);
        analysis.WordCount = words.Length;

        if (analysis.WordCount > 0)
        {
            analysis.AverageWordLength = words.Average(w => w.Length);
        }

        if (lines.Length > 0)
        {
            analysis.AverageWordsPerLine = (double)analysis.WordCount / lines.Length;
        }

        // Sentence analysis (approximate)
        string[] sentences = text.Split(['.', '!', '?'], 
            StringSplitOptions.RemoveEmptyEntries);
        analysis.SentenceCount = sentences.Count(s => !string.IsNullOrWhiteSpace(s));

        if (analysis.SentenceCount > 0)
        {
            analysis.AverageWordsPerSentence = (double)analysis.WordCount / analysis.SentenceCount;
        }

        // Paragraph analysis (double newlines)
        string[] paragraphs = text.Split(["\n\n", "\r\n\r\n"], 
            StringSplitOptions.RemoveEmptyEntries);
        analysis.ParagraphCount = paragraphs.Length;

        // Word frequency analysis (top 10)
        Dictionary<string, int> wordFrequency = words
            .Where(w => w.Length > 3) // Ignore very short words
            .GroupBy(w => w.ToLowerInvariant())
            .OrderByDescending(g => g.Count())
            .Take(10)
            .ToDictionary(g => g.Key, g => g.Count());

        analysis.TopWords = wordFrequency;

        // Calculate readability scores
        if (analysis.SentenceCount <= 0 || analysis.WordCount <= 0) return;
        // Flesch Reading Ease approximation
        double avgSentenceLength = (double)analysis.WordCount / analysis.SentenceCount;
        double avgSyllablesPerWord = analysis.AverageWordLength / 2.5; // Rough approximation
        analysis.ReadabilityScore = 206.835 - 1.015 * avgSentenceLength - 84.6 * avgSyllablesPerWord;
    }
}
