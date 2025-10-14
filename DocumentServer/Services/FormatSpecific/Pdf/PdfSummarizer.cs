using System.Text;
using DocumentServer.Models.Common;
using DocumentServer.Services.Core;
using DocumentServer.Services.FormatSpecific.Pdf.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace DocumentServer.Services.FormatSpecific.Pdf;

/// <summary>
/// Service for summarizing PDF document content
/// </summary>
public class PdfSummarizer(
    ILogger<PdfSummarizer> logger,
    DocumentCache cache,
    PasswordManager passwordManager)
{
    /// <summary>
    /// Generate a summary of the entire document
    /// </summary>
    public async Task<ServiceResult<DocumentSummary>> SummarizeDocumentAsync(
        string filePath,
        int maxLength = 500)
    {
        logger.LogInformation("Summarizing document: {FilePath}, MaxLength: {MaxLength}",
            filePath, maxLength);

        try
        {
            using PdfDocument pdf = await OpenPdfAsync(filePath);

            string fullText = ExtractFullText(pdf);
            DocumentSummary summary = GenerateSummary(fullText, maxLength);

            logger.LogInformation("Generated summary: {WordCount} words, {KeyPoints} key points",
                summary.WordCount, summary.KeyPoints.Count);

            return ServiceResult<DocumentSummary>.CreateSuccess(summary);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error summarizing document: {FilePath}", filePath);
            return ServiceResult<DocumentSummary>.CreateFailure(ex);
        }
    }

    /// <summary>
    /// Summarize a specific page
    /// </summary>
    public async Task<ServiceResult<DocumentSummary>> SummarizePageAsync(
        string filePath,
        int pageNumber,
        int maxLength = 300)
    {
        logger.LogInformation("Summarizing page {Page} from: {FilePath}", pageNumber, filePath);

        try
        {
            using PdfDocument pdf = await OpenPdfAsync(filePath);

            if (pageNumber < 1 || pageNumber > pdf.NumberOfPages)
            {
                return ServiceResult<DocumentSummary>.CreateFailure(
                    $"Page {pageNumber} not found (document has {pdf.NumberOfPages} pages)");
            }

            Page page = pdf.GetPage(pageNumber);
            string pageText = ContentOrderTextExtractor.GetText(page);

            DocumentSummary summary = GenerateSummary(pageText, maxLength);

            logger.LogInformation("Generated page summary: {WordCount} words",
                summary.WordCount);

            return ServiceResult<DocumentSummary>.CreateSuccess(summary);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error summarizing page {Page} from: {FilePath}",
                pageNumber, filePath);
            return ServiceResult<DocumentSummary>.CreateFailure(ex);
        }
    }

    /// <summary>
    /// Summarize a range of pages
    /// </summary>
    public async Task<ServiceResult<DocumentSummary>> SummarizePageRangeAsync(
        string filePath,
        int startPage,
        int endPage,
        int maxLength = 500)
    {
        logger.LogInformation("Summarizing pages {Start}-{End} from: {FilePath}",
            startPage, endPage, filePath);

        try
        {
            using PdfDocument pdf = await OpenPdfAsync(filePath);

            if (startPage < 1 || endPage > pdf.NumberOfPages || startPage > endPage)
            {
                return ServiceResult<DocumentSummary>.CreateFailure(
                    $"Invalid page range {startPage}-{endPage} (document has {pdf.NumberOfPages} pages)");
            }

            var textBuilder = new StringBuilder();

            for (int i = startPage; i <= endPage; i++)
            {
                Page page = pdf.GetPage(i);
                string pageText = ContentOrderTextExtractor.GetText(page);
                textBuilder.AppendLine(pageText);
            }

            var fullText = textBuilder.ToString();
            DocumentSummary summary = GenerateSummary(fullText, maxLength);

            logger.LogInformation("Generated range summary: {WordCount} words from {PageCount} pages",
                summary.WordCount, endPage - startPage + 1);

            return ServiceResult<DocumentSummary>.CreateSuccess(summary);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error summarizing page range {Start}-{End} from: {FilePath}",
                startPage, endPage, filePath);
            return ServiceResult<DocumentSummary>.CreateFailure(ex);
        }
    }

    /// <summary>
    /// Extract keywords from the document
    /// </summary>
    public async Task<ServiceResult<Dictionary<string, int>>> ExtractKeywordsAsync(
        string filePath,
        int topCount = 20)
    {
        logger.LogInformation("Extracting top {Count} keywords from: {FilePath}",
            topCount, filePath);

        try
        {
            using PdfDocument pdf = await OpenPdfAsync(filePath);

            string fullText = ExtractFullText(pdf);
            Dictionary<string, int> keywords = ExtractTopKeywords(fullText, topCount);

            logger.LogInformation("Extracted {Count} keywords", keywords.Count);

            return ServiceResult<Dictionary<string, int>>.CreateSuccess(keywords);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error extracting keywords from: {FilePath}", filePath);
            return ServiceResult<Dictionary<string, int>>.CreateFailure(ex);
        }
    }

    #region Private Methods

    private async Task<PdfDocument> OpenPdfAsync(string filePath)
    {
        return await Task.Run(() =>
        {
            LoadedDocument? cached = cache.Get(filePath);
            var pdf = cached?.DocumentObject as PdfDocument;

            if (pdf is not null)
            {
                return pdf;
            }

            string? password = passwordManager.GetPasswordForFile(filePath);

            if (password is not null)
            {
                logger.LogDebug("Using password for encrypted PDF: {FilePath}", filePath);
                return PdfDocument.Open(filePath, new ParsingOptions { Password = password });
            }

            return PdfDocument.Open(filePath);
        });
    }

    private static string ExtractFullText(PdfDocument pdf)
    {
        var textBuilder = new StringBuilder();

        foreach (Page page in pdf.GetPages())
        {
            string pageText = ContentOrderTextExtractor.GetText(page);
            textBuilder.AppendLine(pageText);
        }

        return textBuilder.ToString();
    }

    private static DocumentSummary GenerateSummary(string fullText, int maxLength)
    {
        string[] words = fullText.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries);
        List<string> sentences = fullText.Split(['.', '!', '?'], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 20)
            .ToList();

        Dictionary<string, int> wordFreq = words
            .Where(w => w.Length > 3)
            .GroupBy(w => w.ToLower())
            .ToDictionary(g => g.Key, g => g.Count());

        List<string> keyPoints = sentences
            .OrderByDescending(s => GetSentenceScore(s, wordFreq))
            .Take(5)
            .ToList();

        string mainContent = string.Join(" ", keyPoints);
        if (mainContent.Length > maxLength)
        {
            mainContent = mainContent[..maxLength];
        }

        var summary = new DocumentSummary
        {
            WordCount = words.Length,
            KeyPoints = keyPoints,
            KeywordFrequency = wordFreq.OrderByDescending(kv => kv.Value).Take(10).ToDictionary(kv => kv.Key, kv => kv.Value),
            MainContent = mainContent
        };

        return summary;
    }

    private static double GetSentenceScore(string sentence, Dictionary<string, int> wordFreq)
    {
        string[] words = sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return 0;

        return words.Where(w => wordFreq.ContainsKey(w.ToLower())).Sum(w => wordFreq[w.ToLower()]) / (double)words.Length;
    }

    private static Dictionary<string, int> ExtractTopKeywords(string text, int topCount)
    {
        string[] words = text.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries);

        Dictionary<string, int> wordFreq = words
            .Where(w => w.Length > 3)
            .GroupBy(w => w.ToLower())
            .ToDictionary(g => g.Key, g => g.Count());

        return wordFreq
            .OrderByDescending(kv => kv.Value)
            .Take(topCount)
            .ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    #endregion
}