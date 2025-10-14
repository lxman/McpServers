using DocumentServer.Models.Common;
using DocumentServer.Services.Core;
using DocumentServer.Services.FormatSpecific.Pdf.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace DocumentServer.Services.FormatSpecific.Pdf;

/// <summary>
/// Service for reading specific pages from PDF documents
/// </summary>
public class PdfPageReader(
    ILogger<PdfPageReader> logger,
    DocumentCache cache,
    PasswordManager passwordManager)
{
    /// <summary>
    /// Read a specific page
    /// </summary>
    public async Task<ServiceResult<PdfPageInfo>> ReadPageAsync(string filePath, int pageNumber)
    {
        logger.LogInformation("Reading page {Page} from: {FilePath}", pageNumber, filePath);

        try
        {
            using PdfDocument pdf = await OpenPdfAsync(filePath);

            if (pageNumber < 1 || pageNumber > pdf.NumberOfPages)
            {
                return ServiceResult<PdfPageInfo>.CreateFailure(
                    $"Page {pageNumber} not found (document has {pdf.NumberOfPages} pages)");
            }

            Page page = pdf.GetPage(pageNumber);
            PdfPageInfo pageInfo = ExtractPageInfo(page);

            logger.LogInformation("Successfully read page {Page}: {Chars} characters",
                pageNumber, pageInfo.CharacterCount);

            return ServiceResult<PdfPageInfo>.CreateSuccess(pageInfo);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading page {Page} from: {FilePath}", pageNumber, filePath);
            return ServiceResult<PdfPageInfo>.CreateFailure(ex);
        }
    }

    /// <summary>
    /// Read a range of pages
    /// </summary>
    public async Task<ServiceResult<List<PdfPageInfo>>> ReadPageRangeAsync(
        string filePath,
        int startPage,
        int endPage)
    {
        logger.LogInformation("Reading pages {Start}-{End} from: {FilePath}",
            startPage, endPage, filePath);

        try
        {
            using PdfDocument pdf = await OpenPdfAsync(filePath);

            if (startPage < 1 || endPage > pdf.NumberOfPages || startPage > endPage)
            {
                return ServiceResult<List<PdfPageInfo>>.CreateFailure(
                    $"Invalid page range {startPage}-{endPage} (document has {pdf.NumberOfPages} pages)");
            }

            var pages = new List<PdfPageInfo>();

            for (int i = startPage; i <= endPage; i++)
            {
                Page page = pdf.GetPage(i);
                PdfPageInfo pageInfo = ExtractPageInfo(page);
                pages.Add(pageInfo);
            }

            logger.LogInformation("Successfully read {Count} pages", pages.Count);

            return ServiceResult<List<PdfPageInfo>>.CreateSuccess(pages);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading page range {Start}-{End} from: {FilePath}",
                startPage, endPage, filePath);
            return ServiceResult<List<PdfPageInfo>>.CreateFailure(ex);
        }
    }

    /// <summary>
    /// Get all pages info
    /// </summary>
    public async Task<ServiceResult<List<PdfPageInfo>>> ReadAllPagesAsync(string filePath)
    {
        logger.LogInformation("Reading all pages from: {FilePath}", filePath);

        try
        {
            using PdfDocument pdf = await OpenPdfAsync(filePath);

            var pages = new List<PdfPageInfo>();

            foreach (Page page in pdf.GetPages())
            {
                PdfPageInfo pageInfo = ExtractPageInfo(page);
                pages.Add(pageInfo);
            }

            logger.LogInformation("Successfully read all {Count} pages", pages.Count);

            return ServiceResult<List<PdfPageInfo>>.CreateSuccess(pages);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading all pages from: {FilePath}", filePath);
            return ServiceResult<List<PdfPageInfo>>.CreateFailure(ex);
        }
    }

    /// <summary>
    /// Get page count
    /// </summary>
    public async Task<ServiceResult<int>> GetPageCountAsync(string filePath)
    {
        logger.LogInformation("Getting page count from: {FilePath}", filePath);

        try
        {
            using PdfDocument pdf = await OpenPdfAsync(filePath);

            int count = pdf.NumberOfPages;

            logger.LogInformation("Document has {Count} pages", count);

            return ServiceResult<int>.CreateSuccess(count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting page count from: {FilePath}", filePath);
            return ServiceResult<int>.CreateFailure(ex);
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

    private static PdfPageInfo ExtractPageInfo(Page page)
    {
        string text = ContentOrderTextExtractor.GetText(page);
        int imageCount = page.GetImages().Count();

        return new PdfPageInfo
        {
            PageNumber = page.Number,
            Text = text,
            Width = page.Width,
            Height = page.Height,
            Rotation = page.Rotation.Value,
            ImageCount = imageCount,
            CharacterCount = text.Length,
            WordCount = text.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries).Length
        };
    }

    #endregion
}