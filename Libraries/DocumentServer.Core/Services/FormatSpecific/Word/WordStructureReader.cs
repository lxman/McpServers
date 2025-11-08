using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentServer.Core.Models.Common;
using DocumentServer.Core.Services.Core;
using DocumentServer.Core.Services.FormatSpecific.Word.Models;
using Microsoft.Extensions.Logging;
using MsOfficeCrypto;

namespace DocumentServer.Core.Services.FormatSpecific.Word;

/// <summary>
/// Service for reading document structure (headings, sections) from Word documents
/// </summary>
public class WordStructureReader(
    ILogger<WordStructureReader> logger,
    DocumentCache cache,
    PasswordManager passwordManager)
{
    /// <summary>
    /// Extract all sections and headings from a Word document
    /// </summary>
    public async Task<ServiceResult<List<WordSection>>> ExtractSectionsAsync(string filePath)
    {
        logger.LogInformation("Extracting sections from: {FilePath}", filePath);

        try
        {
            using WordprocessingDocument doc = await OpenDocumentAsync(filePath);
            Body? body = doc.MainDocumentPart?.Document?.Body;

            if (body is null)
            {
                return ServiceResult<List<WordSection>>.CreateFailure("Document body not found");
            }

            var sections = new List<WordSection>();
            var currentSection = new WordSection { Title = "Main Document", Level = 0 };
            var currentSectionContent = new StringBuilder();

            foreach (OpenXmlElement element in body.Elements())
            {
                if (element is Paragraph paragraph)
                {
                    ProcessParagraph(paragraph, sections, ref currentSection, ref currentSectionContent);
                }
            }

            // Add the final section if it has content
            if (currentSectionContent.Length > 0)
            {
                currentSection.Content = currentSectionContent.ToString();
                sections.Add(currentSection);
            }

            logger.LogInformation("Extracted {Count} sections from: {FilePath}", sections.Count, filePath);

            return ServiceResult<List<WordSection>>.CreateSuccess(sections);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error extracting sections from: {FilePath}", filePath);
            return ServiceResult<List<WordSection>>.CreateFailure(ex);
        }
    }

    /// <summary>
    /// Extract only headings (table of contents) from a Word document
    /// </summary>
    public async Task<ServiceResult<List<WordSection>>> ExtractHeadingsAsync(string filePath)
    {
        logger.LogInformation("Extracting headings from: {FilePath}", filePath);

        try
        {
            using WordprocessingDocument doc = await OpenDocumentAsync(filePath);
            Body? body = doc.MainDocumentPart?.Document?.Body;

            if (body is null)
            {
                return ServiceResult<List<WordSection>>.CreateFailure("Document body not found");
            }

            var headings = new List<WordSection>();

            foreach (Paragraph paragraph in body.Elements<Paragraph>())
            {
                string paragraphText = GetParagraphText(paragraph);
                if (string.IsNullOrWhiteSpace(paragraphText)) continue;

                string? styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
                bool isHeading = IsHeadingStyle(styleId, paragraphText);

                if (isHeading)
                {
                    headings.Add(new WordSection
                    {
                        Title = paragraphText.Trim(),
                        Level = GetHeadingLevel(styleId),
                        Content = "" // Headings don't have content in this context
                    });
                }
            }

            logger.LogInformation("Extracted {Count} headings from: {FilePath}", headings.Count, filePath);

            return ServiceResult<List<WordSection>>.CreateSuccess(headings);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error extracting headings from: {FilePath}", filePath);
            return ServiceResult<List<WordSection>>.CreateFailure(ex);
        }
    }

    /// <summary>
    /// Get content of a specific section by heading title
    /// </summary>
    public async Task<ServiceResult<WordSection>> GetSectionByTitleAsync(string filePath, string headingTitle)
    {
        logger.LogInformation("Getting section '{Title}' from: {FilePath}", headingTitle, filePath);

        try
        {
            ServiceResult<List<WordSection>> sectionsResult = await ExtractSectionsAsync(filePath);
            if (!sectionsResult.Success)
            {
                return ServiceResult<WordSection>.CreateFailure(sectionsResult.Error!);
            }

            WordSection? section = sectionsResult.Data!.FirstOrDefault(s => 
                s.Title.Equals(headingTitle, StringComparison.OrdinalIgnoreCase));

            if (section is null)
            {
                return ServiceResult<WordSection>.CreateFailure($"Section '{headingTitle}' not found");
            }

            logger.LogInformation("Found section '{Title}' with {Length} characters", 
                section.Title, section.Content.Length);

            return ServiceResult<WordSection>.CreateSuccess(section);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting section by title from: {FilePath}", filePath);
            return ServiceResult<WordSection>.CreateFailure(ex);
        }
    }

    #region Private Methods

    private async Task<WordprocessingDocument> OpenDocumentAsync(string filePath)
    {
        LoadedDocument? cached = cache.Get(filePath);
        var doc = cached?.DocumentObject as WordprocessingDocument;

        if (doc is not null)
        {
            return doc;
        }

        string? password = passwordManager.GetPasswordForFile(filePath);
        
        // Use MsOfficeCrypto to handle decryption (or pass through if not encrypted)
        await using FileStream fileStream = File.OpenRead(filePath);
        await using Stream decryptedStream = await OfficeDocument.DecryptAsync(fileStream, password);
        
        // Copy to memory stream and open document
        var memoryStream = new MemoryStream();
        await decryptedStream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        return WordprocessingDocument.Open(memoryStream, false);
    }
    private static void ProcessParagraph(Paragraph paragraph, List<WordSection> sections, 
        ref WordSection currentSection, ref StringBuilder currentSectionContent)
    {
        string paragraphText = GetParagraphText(paragraph);
        if (string.IsNullOrWhiteSpace(paragraphText)) return;

        // Check if this is a heading
        string? styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        bool isHeading = IsHeadingStyle(styleId, paragraphText);

        if (isHeading)
        {
            // Save the previous section if it has content
            if (currentSectionContent.Length > 0)
            {
                currentSection.Content = currentSectionContent.ToString();
                sections.Add(currentSection);
            }

            // Start a new section
            currentSection = new WordSection
            {
                Title = paragraphText.Trim(),
                Level = GetHeadingLevel(styleId),
            };
            currentSectionContent = new StringBuilder();
        }
        else
        {
            // Regular paragraph - add to current section
            currentSectionContent.AppendLine(paragraphText);
        }
    }

    private static string GetParagraphText(Paragraph paragraph)
    {
        var textBuilder = new StringBuilder();

        foreach (Run run in paragraph.Elements<Run>())
        {
            foreach (OpenXmlElement element in run.Elements())
            {
                switch (element)
                {
                    case Text text:
                        textBuilder.Append(text.Text);
                        break;
                    case TabChar _:
                        textBuilder.Append('\t');
                        break;
                    case Break br:
                        textBuilder.Append(br.Type?.Value == BreakValues.Page ? "\n[PAGE BREAK]\n" : "\n");
                        break;
                }
            }
        }

        return textBuilder.ToString();
    }

    private static bool IsHeadingStyle(string? styleId, string text)
    {
        if (string.IsNullOrEmpty(styleId))
        {
            // Heuristic: check for heading-like formatting
            return text.Trim().Length < 100 &&
                   (text.Contains("CHAPTER") || text.Contains("SECTION") ||
                    text.All(c => char.IsUpper(c) || char.IsWhiteSpace(c) || char.IsDigit(c) || c == '.'));
        }

        return styleId.StartsWith("Heading", StringComparison.OrdinalIgnoreCase) ||
               styleId.StartsWith("Title", StringComparison.OrdinalIgnoreCase) ||
               styleId.Contains("Header", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetHeadingLevel(string? styleId)
    {
        if (string.IsNullOrEmpty(styleId)) return 1;

        // Extract number from style like "Heading1", "Heading2", etc.
        for (var i = 1; i <= 9; i++)
        {
            if (styleId.Contains(i.ToString()))
                return i;
        }

        return 1;
    }

    #endregion
}