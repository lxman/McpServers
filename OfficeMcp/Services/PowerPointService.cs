using System.Text;
using Microsoft.Extensions.Logging;
using OfficeMcp.Models.PowerPoint;
using ShapeCrawler;

namespace OfficeMcp.Services;

public interface IPowerPointService
{
    Task<PowerPointContent> LoadPowerPointContentAsync(string filePath, string? password = null);
}

public class PowerPointService(
    IDocumentDecryptionService decryptionService, 
    ILogger<PowerPointService> logger) : IPowerPointService
{
    public async Task<PowerPointContent> LoadPowerPointContentAsync(string filePath, string? password = null)
    {
        var powerPointContent = new PowerPointContent();
        
        try
        {
            // Get the decrypted stream first
            await using FileStream fileStream = File.OpenRead(filePath);
            await using Stream decryptedStream = await decryptionService.DecryptDocumentAsync(fileStream, password);
            
            string fileExtension = Path.GetExtension(filePath).ToLowerInvariant();

            return fileExtension switch
            {
                ".pptx" => await LoadPptxContentAsync(decryptedStream, logger),
                _ => throw new NotSupportedException($"Unsupported PowerPoint format: {fileExtension}")
            };
        }
        catch (NotSupportedException)
        {
            // Re-throw NotSupportedException as-is
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading PowerPoint content from: {FilePath}", filePath);
            powerPointContent.PlainText = $"Error loading PowerPoint document: {ex.Message}";
            return powerPointContent;
        }
    }

    private static async Task<PowerPointContent> LoadPptxContentAsync(Stream documentStream, ILogger logger)
    {
        return await Task.Run(() =>
        {
            var powerPointContent = new PowerPointContent();
            var textBuilder = new StringBuilder();
            var slides = new List<PowerPointSlide>();
            
            try
            {
                logger.LogInformation("Stream position: {Position}, CanSeek: {CanSeek}, Length: {Length}", 
                    documentStream.Position, documentStream.CanSeek, 
                    documentStream.CanSeek ? documentStream.Length : -1);

                // Ensure stream is at position 0
                if (documentStream.CanSeek)
                {
                    documentStream.Position = 0;
                }

                // ShapeCrawler might need the stream to be copied to a new MemoryStream
                MemoryStream memStream;
                if (documentStream is MemoryStream ms)
                {
                    memStream = ms;
                }
                else
                {
                    memStream = new MemoryStream();
                    documentStream.CopyTo(memStream);
                    memStream.Position = 0;
                }

                logger.LogInformation("About to create Presentation object from stream");

                // ShapeCrawler provides a much simpler API than raw OpenXML
                using var presentation = new Presentation(memStream);
                
                logger.LogInformation("Presentation created. Slide count: {Count}", presentation.Slides.Count);

                if (presentation.Slides.Count == 0)
                {
                    textBuilder.AppendLine("PowerPoint presentation contains no slides.");
                    powerPointContent.PlainText = textBuilder.ToString();
                    return powerPointContent;
                }

                var slideNumber = 1;
                foreach (ISlide slide in presentation.Slides)
                {
                    logger.LogInformation("Processing slide {Number}, Shapes count: {ShapeCount}", 
                        slideNumber, slide.Shapes.Count);

                    PowerPointSlide slideModel = ProcessSlide(slide, slideNumber, textBuilder, logger);
                    slides.Add(slideModel);
                    slideNumber++;
                }
                
                powerPointContent.PlainText = textBuilder.ToString();
                powerPointContent.Slides = slides;
                powerPointContent.SlideCount = slides.Count;

                logger.LogInformation("Successfully extracted {SlideCount} slides, total text length: {TextLength}", 
                    slides.Count, textBuilder.Length);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error reading PowerPoint document");
                textBuilder.AppendLine($"Error reading PowerPoint document: {ex.Message}");
                textBuilder.AppendLine($"Stack trace: {ex.StackTrace}");
                powerPointContent.PlainText = textBuilder.ToString();
            }
            
            return powerPointContent;
        });
    }

    private static PowerPointSlide ProcessSlide(ISlide slide, int slideNumber, StringBuilder textBuilder, ILogger logger)
    {
        var slideModel = new PowerPointSlide
        {
            SlideNumber = slideNumber,
            Title = string.Empty
        };

        var slideTextBuilder = new StringBuilder();
        
        textBuilder.AppendLine($"\n=== Slide {slideNumber} ===");

        // Extract text from all shapes
        var isFirstShape = true;
        foreach (IShape shape in slide.Shapes)
        {
            logger.LogDebug("Shape type: {ShapeType}, HasTextBox: {HasTextBox}", 
                shape.GetType().Name, shape.TextBox != null);

            // Try to get text from the shape
            string? shapeText = GetShapeText(shape, logger);

            if (string.IsNullOrWhiteSpace(shapeText))
            {
                logger.LogDebug("Shape has no text content");
                continue;
            }

            logger.LogDebug("Shape text extracted: {Text}", shapeText.Substring(0, Math.Min(50, shapeText.Length)));

            // The first non-empty text is typically the title
            if (isFirstShape && string.IsNullOrEmpty(slideModel.Title))
            {
                slideModel.Title = shapeText.Trim();
                isFirstShape = false;
            }
                
            slideTextBuilder.AppendLine(shapeText);
            textBuilder.AppendLine(shapeText);
        }

        // Extract speaker notes if available
        if (slide.Notes != null && !string.IsNullOrWhiteSpace(slide.Notes.Text))
        {
            slideModel.Notes = slide.Notes.Text.Trim();
            textBuilder.AppendLine($"\n[Speaker Notes]\n{slide.Notes.Text}");
        }

        slideModel.Content = slideTextBuilder.ToString().Trim();
        
        // Default title if none found
        if (string.IsNullOrEmpty(slideModel.Title))
        {
            slideModel.Title = $"Slide {slideNumber}";
        }

        return slideModel;
    }

    private static string? GetShapeText(IShape shape, ILogger logger)
    {
        try
        {
            // Check for the table first using the Table property
            if (shape.Table is null)
            {
                string? text = shape.TextBox?.Text;
                logger.LogDebug("TextBox content: {HasText}, Length: {Length}", 
                    !string.IsNullOrEmpty(text), text?.Length ?? 0);
                return text;
            }

            logger.LogDebug("Processing table with {RowCount} rows", shape.Table.Rows.Count);
            var tableText = new StringBuilder();
            foreach (ITableRow row in shape.Table.Rows)
            {
                List<string> rowText = (from cell in row.Cells 
                    where !string.IsNullOrWhiteSpace(cell.TextBox.Text) 
                    select cell.TextBox.Text.Trim()).ToList();
                if (rowText.Count != 0)
                {
                    tableText.AppendLine(string.Join(" | ", rowText));
                }
            }
            return tableText.ToString();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error extracting shape text");
            return null;
        }
    }
}