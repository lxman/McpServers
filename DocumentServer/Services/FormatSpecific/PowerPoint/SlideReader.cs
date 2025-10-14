using System.Text;
using DocumentServer.Models.Common;
using DocumentServer.Services.Core;
using DocumentServer.Services.FormatSpecific.PowerPoint.Models;
using MsOfficeCrypto;
using ShapeCrawler;

namespace DocumentServer.Services.FormatSpecific.PowerPoint;

/// <summary>
/// Service for reading specific slides from PowerPoint presentations
/// </summary>
public class SlideReader(
    ILogger<SlideReader> logger,
    DocumentCache cache,
    PasswordManager passwordManager)
{
    /// <summary>
    /// Read a specific slide by number
    /// </summary>
    public async Task<ServiceResult<PowerPointSlide>> ReadSlideAsync(string filePath, int slideNumber)
    {
        logger.LogInformation("Reading slide #{Number} from: {FilePath}", slideNumber, filePath);

        try
        {
            using Presentation presentation = await OpenPresentationAsync(filePath);

            if (slideNumber < 1 || slideNumber > presentation.Slides.Count)
            {
                return ServiceResult<PowerPointSlide>.CreateFailure(
                    $"Slide {slideNumber} not found (presentation has {presentation.Slides.Count} slides)");
            }

            ISlide slide = presentation.Slides[slideNumber - 1];
            PowerPointSlide slideModel = await ProcessSlideAsync(slide, slideNumber);

            logger.LogInformation("Successfully read slide #{Number}: {Title}", slideNumber, slideModel.Title);

            return ServiceResult<PowerPointSlide>.CreateSuccess(slideModel);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading slide #{Number} from: {FilePath}", slideNumber, filePath);
            return ServiceResult<PowerPointSlide>.CreateFailure(ex);
        }
    }

    /// <summary>
    /// Read a range of slides
    /// </summary>
    public async Task<ServiceResult<List<PowerPointSlide>>> ReadSlideRangeAsync(
        string filePath, 
        int startSlide, 
        int endSlide)
    {
        logger.LogInformation("Reading slides {Start}-{End} from: {FilePath}", 
            startSlide, endSlide, filePath);

        try
        {
            using Presentation presentation = await OpenPresentationAsync(filePath);

            if (startSlide < 1 || endSlide > presentation.Slides.Count || startSlide > endSlide)
            {
                return ServiceResult<List<PowerPointSlide>>.CreateFailure(
                    $"Invalid slide range {startSlide}-{endSlide} (presentation has {presentation.Slides.Count} slides)");
            }

            var slides = new List<PowerPointSlide>();

            for (int i = startSlide - 1; i < endSlide; i++)
            {
                ISlide slide = presentation.Slides[i];
                PowerPointSlide slideModel = await ProcessSlideAsync(slide, i + 1);
                slides.Add(slideModel);
            }

            logger.LogInformation("Successfully read {Count} slides", slides.Count);

            return ServiceResult<List<PowerPointSlide>>.CreateSuccess(slides);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading slide range {Start}-{End} from: {FilePath}", 
                startSlide, endSlide, filePath);
            return ServiceResult<List<PowerPointSlide>>.CreateFailure(ex);
        }
    }

    /// <summary>
    /// Get all slides from presentation
    /// </summary>
    public async Task<ServiceResult<List<PowerPointSlide>>> ReadAllSlidesAsync(string filePath)
    {
        logger.LogInformation("Reading all slides from: {FilePath}", filePath);

        try
        {
            using Presentation presentation = await OpenPresentationAsync(filePath);

            var slides = new List<PowerPointSlide>();
            var slideNumber = 1;

            foreach (ISlide slide in presentation.Slides)
            {
                PowerPointSlide slideModel = await ProcessSlideAsync(slide, slideNumber++);
                slides.Add(slideModel);
            }

            logger.LogInformation("Successfully read all {Count} slides", slides.Count);

            return ServiceResult<List<PowerPointSlide>>.CreateSuccess(slides);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading all slides from: {FilePath}", filePath);
            return ServiceResult<List<PowerPointSlide>>.CreateFailure(ex);
        }
    }

    /// <summary>
    /// Get slide count
    /// </summary>
    public async Task<ServiceResult<int>> GetSlideCountAsync(string filePath)
    {
        logger.LogInformation("Getting slide count from: {FilePath}", filePath);

        try
        {
            using Presentation presentation = await OpenPresentationAsync(filePath);

            int count = presentation.Slides.Count;

            logger.LogInformation("Presentation has {Count} slides", count);

            return ServiceResult<int>.CreateSuccess(count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting slide count from: {FilePath}", filePath);
            return ServiceResult<int>.CreateFailure(ex);
        }
    }

    /// <summary>
    /// Get slide titles (table of contents)
    /// </summary>
    public async Task<ServiceResult<List<string>>> GetSlideTitlesAsync(string filePath)
    {
        logger.LogInformation("Getting slide titles from: {FilePath}", filePath);

        try
        {
            using Presentation presentation = await OpenPresentationAsync(filePath);

            var titles = new List<string>();
            var slideNumber = 1;

            foreach (ISlide slide in presentation.Slides)
            {
                string title = ExtractSlideTitle(slide, slideNumber);
                titles.Add(title);
                slideNumber++;
            }

            logger.LogInformation("Extracted {Count} slide titles", titles.Count);

            return ServiceResult<List<string>>.CreateSuccess(titles);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting slide titles from: {FilePath}", filePath);
            return ServiceResult<List<string>>.CreateFailure(ex);
        }
    }

    #region Private Methods

    private async Task<Presentation> OpenPresentationAsync(string filePath)
    {
        LoadedDocument? cached = cache.Get(filePath);
        var presentation = cached?.DocumentObject as Presentation;

        if (presentation is not null)
        {
            return presentation;
        }

        string? password = passwordManager.GetPasswordForFile(filePath);

        // Use MsOfficeCrypto to handle decryption (or pass through if not encrypted)
        await using FileStream fileStream = File.OpenRead(filePath);
        await using Stream decryptedStream = await OfficeDocument.DecryptAsync(fileStream, password);
        
        // Copy to memory stream and create presentation
        var memoryStream = new MemoryStream();
        await decryptedStream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        return new Presentation(memoryStream);
    }

    private async Task<PowerPointSlide> ProcessSlideAsync(ISlide slide, int slideNumber)
    {
        return await Task.Run(() =>
        {
            var slideModel = new PowerPointSlide
            {
                SlideNumber = slideNumber,
                Title = string.Empty
            };

            var slideTextBuilder = new StringBuilder();
            var isFirstShape = true;

            // Extract text from all shapes
            foreach (IShape shape in slide.Shapes)
            {
                string? shapeText = GetShapeText(shape);

                if (string.IsNullOrWhiteSpace(shapeText))
                {
                    continue;
                }

                // First non-empty text is typically the title
                if (isFirstShape && string.IsNullOrEmpty(slideModel.Title))
                {
                    slideModel.Title = shapeText.Trim();
                    isFirstShape = false;
                }

                slideTextBuilder.AppendLine(shapeText);
            }

            // Extract speaker notes if available
            if (slide.Notes is not null && !string.IsNullOrWhiteSpace(slide.Notes.Text))
            {
                slideModel.Notes = slide.Notes.Text.Trim();
            }

            slideModel.Content = slideTextBuilder.ToString().Trim();

            // Default title if none found
            if (string.IsNullOrEmpty(slideModel.Title))
            {
                slideModel.Title = $"Slide {slideNumber}";
            }

            return slideModel;
        });
    }

    private string ExtractSlideTitle(ISlide slide, int slideNumber)
    {
        foreach (IShape shape in slide.Shapes)
        {
            string? shapeText = GetShapeText(shape);

            if (!string.IsNullOrWhiteSpace(shapeText))
            {
                return shapeText.Trim();
            }
        }

        return $"Slide {slideNumber}";
    }

    private string? GetShapeText(IShape shape)
    {
        try
        {
            // Check for table first
            if (shape.Table is not null)
            {
                var tableText = new StringBuilder();
                foreach (ITableRow row in shape.Table.Rows)
                {
                    var rowText = new List<string>();
                    foreach (ITableCell cell in row.Cells)
                    {
                        if (!string.IsNullOrWhiteSpace(cell.TextBox.Text))
                        {
                            rowText.Add(cell.TextBox.Text.Trim());
                        }
                    }

                    if (rowText.Count > 0)
                    {
                        tableText.AppendLine(string.Join(" | ", rowText));
                    }
                }
                return tableText.ToString();
            }

            // Otherwise try to get text from text box
            return shape.TextBox?.Text;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error extracting shape text");
            return null;
        }
    }

    #endregion
}
