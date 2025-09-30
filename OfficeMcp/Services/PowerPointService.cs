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
                ".pptx" => await LoadPptxContentAsync(decryptedStream),
                ".ppt" => throw new NotSupportedException(
                    "Legacy .ppt format is not supported. Please convert to .pptx format first. " +
                    "You can convert using PowerPoint: File > Save As > PowerPoint Presentation (.pptx)"),
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

    private static async Task<PowerPointContent> LoadPptxContentAsync(Stream documentStream)
    {
        return await Task.Run(() =>
        {
            var powerPointContent = new PowerPointContent();
            var textBuilder = new StringBuilder();
            var slides = new List<PowerPointSlide>();
            
            try
            {
                // ShapeCrawler provides a much simpler API than raw OpenXML
                using var presentation = new Presentation(documentStream);
                
                var slideNumber = 1;
                foreach (ISlide slide in presentation.Slides)
                {
                    PowerPointSlide slideModel = ProcessSlide(slide, slideNumber, textBuilder);
                    slides.Add(slideModel);
                    slideNumber++;
                }
                
                powerPointContent.PlainText = textBuilder.ToString();
                powerPointContent.Slides = slides;
                powerPointContent.SlideCount = slides.Count;
            }
            catch (Exception ex)
            {
                textBuilder.AppendLine($"Error reading PowerPoint document: {ex.Message}");
                powerPointContent.PlainText = textBuilder.ToString();
            }
            
            return powerPointContent;
        });
    }

    private static PowerPointSlide ProcessSlide(ISlide slide, int slideNumber, StringBuilder textBuilder)
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
            // Try to get text from the shape
            string? shapeText = GetShapeText(shape);

            if (string.IsNullOrWhiteSpace(shapeText)) continue;
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
            textBuilder.AppendLine($"\n[Speaker Notes]\n{slide.Notes}");
        }

        slideModel.Content = slideTextBuilder.ToString().Trim();
        
        // Default title if none found
        if (string.IsNullOrEmpty(slideModel.Title))
        {
            slideModel.Title = $"Slide {slideNumber}";
        }

        return slideModel;
    }

    private static string? GetShapeText(IShape shape)
    {
        try
        {
            // Check for the table first using the Table property
            if (shape.Table is null) return shape.TextBox?.Text;
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
        catch
        {
            return null;
        }
    }
}