using DocumentServer.Core.Models.Common;
using DocumentServer.Core.Services.Core;
using Microsoft.Extensions.Logging;
using MsOfficeCrypto;
using ShapeCrawler;

namespace DocumentServer.Core.Services.FormatSpecific.PowerPoint;

/// <summary>
/// Service for extracting speaker notes from PowerPoint presentations
/// </summary>
public class NotesExtractor(
    ILogger<NotesExtractor> logger,
    DocumentCache cache,
    PasswordManager passwordManager)
{
    /// <summary>
    /// Extract notes from a specific slide
    /// </summary>
    public async Task<ServiceResult<string>> ExtractSlideNotesAsync(string filePath, int slideNumber)
    {
        logger.LogInformation("Extracting notes from slide #{Number} in: {FilePath}", slideNumber, filePath);

        try
        {
            using var presentation = await OpenPresentationAsync(filePath);

            if (slideNumber < 1 || slideNumber > presentation.Slides.Count)
            {
                return ServiceResult<string>.CreateFailure(
                    $"Slide {slideNumber} not found (presentation has {presentation.Slides.Count} slides)");
            }

            var slide = presentation.Slides[slideNumber - 1];
            var notes = ExtractNotes(slide);

            logger.LogInformation("Extracted {Length} characters of notes from slide #{Number}",
                notes.Length, slideNumber);

            return ServiceResult<string>.CreateSuccess(notes);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error extracting notes from slide #{Number} in: {FilePath}",
                slideNumber, filePath);
            return ServiceResult<string>.CreateFailure(ex);
        }
    }

    /// <summary>
    /// Extract all notes from the presentation
    /// </summary>
    public async Task<ServiceResult<Dictionary<int, string>>> ExtractAllNotesAsync(string filePath)
    {
        logger.LogInformation("Extracting all notes from: {FilePath}", filePath);

        try
        {
            using var presentation = await OpenPresentationAsync(filePath);

            var allNotes = new Dictionary<int, string>();
            var slideNumber = 1;

            foreach (var slide in presentation.Slides)
            {
                var notes = ExtractNotes(slide);

                if (!string.IsNullOrWhiteSpace(notes))
                {
                    allNotes[slideNumber] = notes;
                }

                slideNumber++;
            }

            logger.LogInformation("Extracted notes from {Count} slides (out of {Total})",
                allNotes.Count, presentation.Slides.Count);

            return ServiceResult<Dictionary<int, string>>.CreateSuccess(allNotes);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error extracting all notes from: {FilePath}", filePath);
            return ServiceResult<Dictionary<int, string>>.CreateFailure(ex);
        }
    }

    /// <summary>
    /// Check if a slide has notes
    /// </summary>
    public async Task<ServiceResult<bool>> HasNotesAsync(string filePath, int slideNumber)
    {
        logger.LogInformation("Checking if slide #{Number} has notes in: {FilePath}", 
            slideNumber, filePath);

        try
        {
            using var presentation = await OpenPresentationAsync(filePath);

            if (slideNumber < 1 || slideNumber > presentation.Slides.Count)
            {
                return ServiceResult<bool>.CreateFailure(
                    $"Slide {slideNumber} not found (presentation has {presentation.Slides.Count} slides)");
            }

            var slide = presentation.Slides[slideNumber - 1];
            var hasNotes = slide.Notes is not null && !string.IsNullOrWhiteSpace(slide.Notes.Text);

            logger.LogInformation("Slide #{Number} has notes: {HasNotes}", slideNumber, hasNotes);

            return ServiceResult<bool>.CreateSuccess(hasNotes);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking notes for slide #{Number} in: {FilePath}",
                slideNumber, filePath);
            return ServiceResult<bool>.CreateFailure(ex);
        }
    }

    /// <summary>
    /// Get list of slide numbers that have notes
    /// </summary>
    public async Task<ServiceResult<List<int>>> GetSlidesWithNotesAsync(string filePath)
    {
        logger.LogInformation("Getting slides with notes from: {FilePath}", filePath);

        try
        {
            using var presentation = await OpenPresentationAsync(filePath);

            var slidesWithNotes = new List<int>();
            var slideNumber = 1;

            foreach (var slide in presentation.Slides)
            {
                if (slide.Notes is not null && !string.IsNullOrWhiteSpace(slide.Notes.Text))
                {
                    slidesWithNotes.Add(slideNumber);
                }

                slideNumber++;
            }

            logger.LogInformation("Found notes in {Count} slides", slidesWithNotes.Count);

            return ServiceResult<List<int>>.CreateSuccess(slidesWithNotes);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting slides with notes from: {FilePath}", filePath);
            return ServiceResult<List<int>>.CreateFailure(ex);
        }
    }

    /// <summary>
    /// Extract notes from a range of slides
    /// </summary>
    public async Task<ServiceResult<Dictionary<int, string>>> ExtractNotesRangeAsync(
        string filePath,
        int startSlide,
        int endSlide)
    {
        logger.LogInformation("Extracting notes from slides {Start}-{End} in: {FilePath}",
            startSlide, endSlide, filePath);

        try
        {
            using var presentation = await OpenPresentationAsync(filePath);

            if (startSlide < 1 || endSlide > presentation.Slides.Count || startSlide > endSlide)
            {
                return ServiceResult<Dictionary<int, string>>.CreateFailure(
                    $"Invalid slide range {startSlide}-{endSlide} (presentation has {presentation.Slides.Count} slides)");
            }

            var notes = new Dictionary<int, string>();

            for (var i = startSlide - 1; i < endSlide; i++)
            {
                var slide = presentation.Slides[i];
                var slideNotes = ExtractNotes(slide);

                if (!string.IsNullOrWhiteSpace(slideNotes))
                {
                    notes[i + 1] = slideNotes;
                }
            }

            logger.LogInformation("Extracted notes from {Count} slides in range {Start}-{End}",
                notes.Count, startSlide, endSlide);

            return ServiceResult<Dictionary<int, string>>.CreateSuccess(notes);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error extracting notes from slides {Start}-{End} in: {FilePath}",
                startSlide, endSlide, filePath);
            return ServiceResult<Dictionary<int, string>>.CreateFailure(ex);
        }
    }

    #region Private Methods

    private async Task<Presentation> OpenPresentationAsync(string filePath)
    {
        var cached = cache.Get(filePath);
        var presentation = cached?.DocumentObject as Presentation;

        if (presentation is not null)
        {
            return presentation;
        }

        var password = passwordManager.GetPasswordForFile(filePath);

        // Use MsOfficeCrypto to handle decryption (or pass through if not encrypted)
        await using var fileStream = File.OpenRead(filePath);
        await using var decryptedStream = await OfficeDocument.DecryptAsync(fileStream, password);
        
        // Copy to memory stream and create presentation
        var memoryStream = new MemoryStream();
        await decryptedStream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        return new Presentation(memoryStream);
    }

    private static string ExtractNotes(ISlide slide)
    {
        if (slide.Notes is null || string.IsNullOrWhiteSpace(slide.Notes.Text))
        {
            return string.Empty;
        }

        return slide.Notes.Text.Trim();
    }

    #endregion
}
