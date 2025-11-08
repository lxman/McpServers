using DocumentServer.Core.Models.Common;
using DocumentServer.Core.Services.Core;
using DocumentServer.Core.Services.FormatSpecific.Pdf.Models;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace DocumentServer.Core.Services.FormatSpecific.Pdf;

/// <summary>
/// Service for extracting images from PDF documents
/// </summary>
public class PdfImageExtractor(
    ILogger<PdfImageExtractor> logger,
    DocumentCache cache,
    PasswordManager passwordManager)
{
    /// <summary>
    /// Extract all images from a specific page
    /// </summary>
    public async Task<ServiceResult<List<PdfImageInfo>>> ExtractPageImagesAsync(
        string filePath,
        int pageNumber,
        bool includeImageData = false)
    {
        logger.LogInformation("Extracting images from page {Page} in: {FilePath}",
            pageNumber, filePath);

        try
        {
            using var pdf = await OpenPdfAsync(filePath);

            if (pageNumber < 1 || pageNumber > pdf.NumberOfPages)
            {
                return ServiceResult<List<PdfImageInfo>>.CreateFailure(
                    $"Page {pageNumber} not found (document has {pdf.NumberOfPages} pages)");
            }

            var page = pdf.GetPage(pageNumber);
            var images = ExtractImagesFromPage(page, includeImageData);

            logger.LogInformation("Extracted {Count} images from page {Page}",
                images.Count, pageNumber);

            return ServiceResult<List<PdfImageInfo>>.CreateSuccess(images);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error extracting images from page {Page} in: {FilePath}",
                pageNumber, filePath);
            return ServiceResult<List<PdfImageInfo>>.CreateFailure(ex);
        }
    }

    /// <summary>
    /// Extract all images from the entire document
    /// </summary>
    public async Task<ServiceResult<List<PdfImageInfo>>> ExtractAllImagesAsync(
        string filePath,
        bool includeImageData = false)
    {
        logger.LogInformation("Extracting all images from: {FilePath}", filePath);

        try
        {
            using var pdf = await OpenPdfAsync(filePath);

            var allImages = new List<PdfImageInfo>();

            foreach (var page in pdf.GetPages())
            {
                var pageImages = ExtractImagesFromPage(page, includeImageData);
                allImages.AddRange(pageImages);
            }

            logger.LogInformation("Extracted {Count} total images from document", allImages.Count);

            return ServiceResult<List<PdfImageInfo>>.CreateSuccess(allImages);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error extracting all images from: {FilePath}", filePath);
            return ServiceResult<List<PdfImageInfo>>.CreateFailure(ex);
        }
    }

    /// <summary>
    /// Get image count for each page
    /// </summary>
    public async Task<ServiceResult<Dictionary<int, int>>> GetImageCountPerPageAsync(string filePath)
    {
        logger.LogInformation("Getting image count per page from: {FilePath}", filePath);

        try
        {
            using var pdf = await OpenPdfAsync(filePath);

            var imageCounts = new Dictionary<int, int>();

            foreach (var page in pdf.GetPages())
            {
                var imageCount = page.GetImages().Count();
                imageCounts[page.Number] = imageCount;
            }

            var totalImages = imageCounts.Values.Sum();
            logger.LogInformation("Document has {Total} images across {Pages} pages",
                totalImages, pdf.NumberOfPages);

            return ServiceResult<Dictionary<int, int>>.CreateSuccess(imageCounts);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting image counts from: {FilePath}", filePath);
            return ServiceResult<Dictionary<int, int>>.CreateFailure(ex);
        }
    }

    /// <summary>
    /// Get total image count in document
    /// </summary>
    public async Task<ServiceResult<int>> GetTotalImageCountAsync(string filePath)
    {
        logger.LogInformation("Getting total image count from: {FilePath}", filePath);

        try
        {
            using var pdf = await OpenPdfAsync(filePath);

            var totalCount = 0;

            foreach (var page in pdf.GetPages())
            {
                totalCount += page.GetImages().Count();
            }

            logger.LogInformation("Document has {Count} total images", totalCount);

            return ServiceResult<int>.CreateSuccess(totalCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting total image count from: {FilePath}", filePath);
            return ServiceResult<int>.CreateFailure(ex);
        }
    }

    /// <summary>
    /// Extract a specific image by ID from a page
    /// </summary>
    public async Task<ServiceResult<PdfImageInfo>> ExtractImageByIdAsync(
        string filePath,
        int pageNumber,
        int imageId)
    {
        logger.LogInformation("Extracting image #{ImageId} from page {Page} in: {FilePath}",
            imageId, pageNumber, filePath);

        try
        {
            using var pdf = await OpenPdfAsync(filePath);

            if (pageNumber < 1 || pageNumber > pdf.NumberOfPages)
            {
                return ServiceResult<PdfImageInfo>.CreateFailure(
                    $"Page {pageNumber} not found (document has {pdf.NumberOfPages} pages)");
            }

            var page = pdf.GetPage(pageNumber);
            var images = page.GetImages().ToList();

            if (imageId < 0 || imageId >= images.Count)
            {
                return ServiceResult<PdfImageInfo>.CreateFailure(
                    $"Image {imageId} not found on page {pageNumber} (page has {images.Count} images)");
            }

            var pdfImage = images[imageId];
            var imageInfo = ConvertToImageInfo(pdfImage, pageNumber, imageId, true);

            logger.LogInformation("Successfully extracted image #{ImageId} from page {Page}",
                imageId, pageNumber);

            return ServiceResult<PdfImageInfo>.CreateSuccess(imageInfo);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error extracting image #{ImageId} from page {Page} in: {FilePath}",
                imageId, pageNumber, filePath);
            return ServiceResult<PdfImageInfo>.CreateFailure(ex);
        }
    }

    #region Private Methods

    private async Task<PdfDocument> OpenPdfAsync(string filePath)
    {
        return await Task.Run(() =>
        {
            var cached = cache.Get(filePath);
            var pdf = cached?.DocumentObject as PdfDocument;

            if (pdf is not null)
            {
                return pdf;
            }

            var password = passwordManager.GetPasswordForFile(filePath);

            if (password is not null)
            {
                logger.LogDebug("Using password for encrypted PDF: {FilePath}", filePath);
                return PdfDocument.Open(filePath, new ParsingOptions { Password = password });
            }

            return PdfDocument.Open(filePath);
        });
    }

    private static List<PdfImageInfo> ExtractImagesFromPage(Page page, bool includeImageData)
    {
        var images = new List<PdfImageInfo>();
        var pdfImages = page.GetImages();
        var imageId = 0;

        foreach (var pdfImage in pdfImages)
        {
            var imageInfo = ConvertToImageInfo(pdfImage, page.Number, imageId, includeImageData);
            images.Add(imageInfo);
            imageId++;
        }

        return images;
    }

    private static PdfImageInfo ConvertToImageInfo(
        IPdfImage pdfImage,
        int pageNumber,
        int imageId,
        bool includeImageData)
    {
        var imageInfo = new PdfImageInfo
        {
            ImageId = imageId,
            Name = $"image_{imageId}",
            PageNumber = pageNumber,
            Width = (int)pdfImage.Bounds.Width,
            Height = (int)pdfImage.Bounds.Height,
            X = pdfImage.Bounds.Left,
            Y = pdfImage.Bounds.Bottom,
            SizeInBytes = pdfImage.RawBytes.Length
        };

        if (includeImageData)
        {
            imageInfo.ImageData = pdfImage.RawBytes.ToArray();
        }

        return imageInfo;
    }

    #endregion
}