using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;
using NPOI.HWPF;
using OfficeMcp.Models.Word;
using Comment = DocumentFormat.OpenXml.Wordprocessing.Comment;
using Paragraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using Range = NPOI.HWPF.UserModel.Range;
using Run = DocumentFormat.OpenXml.Wordprocessing.Run;

namespace OfficeMcp.Services;

public interface IWordService
{
    Task<WordContent> LoadWordContentAsync(string filePath, string? password = null);
}

public class WordService(IDocumentDecryptionService decryptionService, ILogger<WordService> logger) : IWordService
{
    public async Task<WordContent> LoadWordContentAsync(string filePath, string? password)
    {
        var wordContent = new WordContent();
        
        try
        {
            // Get decrypted stream first, then determine processing method
            await using FileStream fileStream = File.OpenRead(filePath);
            await using Stream decryptedStream = await decryptionService.DecryptDocumentAsync(fileStream, password);
            
            string fileExtension = Path.GetExtension(filePath).ToLowerInvariant();

            return fileExtension switch
            {
                ".docx" => await LoadDocxContentAsync(decryptedStream),
                ".doc" => await LoadDocContentAsync(decryptedStream),
                _ => throw new NotSupportedException($"Unsupported Word format: {fileExtension}")
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading Word content from: {FilePath}", filePath);
            wordContent.PlainText = $"Error loading Word document: {ex.Message}";
            return wordContent;
        }
    }

    private static async Task<WordContent> LoadDocxContentAsync(Stream documentStream)
    {
        return await Task.Run(() =>
        {
            var wordContent = new WordContent();
            var textBuilder = new StringBuilder();
            var sections = new List<WordSection>();
            var tables = new List<WordTable>();
            var comments = new List<WordComment>();
            
            try
            {
                using WordprocessingDocument doc = WordprocessingDocument.Open(documentStream, false);
                Body? body = doc.MainDocumentPart?.Document?.Body;
                
                if (body == null)
                {
                    wordContent.PlainText = "Document body not found";
                    return wordContent;
                }

                var currentSection = new WordSection { Title = "Main Document", Level = 0 };
                var currentSectionContent = new StringBuilder();
                
                foreach (OpenXmlElement element in body.Elements())
                {
                    switch (element)
                    {
                        case Paragraph paragraph:
                            ProcessParagraph(paragraph, textBuilder, sections, ref currentSection, ref currentSectionContent);
                            break;
                            
                        case Table table:
                            ProcessTable(table, textBuilder, tables);
                            break;
                            
                        case SectionProperties _:
                            // Section break - finalize current section
                            if (currentSectionContent.Length > 0)
                            {
                                currentSection.Content = currentSectionContent.ToString();
                                sections.Add(currentSection);
                                currentSection = new WordSection { Title = "New Section", Level = 0 };
                                currentSectionContent = new StringBuilder();
                            }
                            break;
                    }
                }
                
                // Add the final section if it has content
                if (currentSectionContent.Length > 0)
                {
                    currentSection.Content = currentSectionContent.ToString();
                    sections.Add(currentSection);
                }
                
                // Extract comments if present
                if (doc.MainDocumentPart?.WordprocessingCommentsPart != null)
                {
                    ProcessComments(doc.MainDocumentPart.WordprocessingCommentsPart, comments);
                }
                
                wordContent.PlainText = textBuilder.ToString();
                wordContent.Sections = sections;
                wordContent.Tables = tables;
                wordContent.Comments = comments;
            }
            catch (Exception ex)
            {
                textBuilder.AppendLine($"Error reading Word document: {ex.Message}");
                wordContent.PlainText = textBuilder.ToString();
            }
            
            return wordContent;
        });
    }
    
    private static async Task<WordContent> LoadDocContentAsync(Stream documentStream)
    {
        return await Task.Run(() =>
        {
            var wordContent = new WordContent();
            var textBuilder = new StringBuilder();
            var sections = new List<WordSection>();
            
            try
            {
                var doc = new HWPFDocument(documentStream);
                
                Range? range = doc.GetRange();
                
                // Extract text content
                for (var i = 0; i < range.NumParagraphs; i++)
                {
                    NPOI.HWPF.UserModel.Paragraph? paragraph = range.GetParagraph(i);
                    string? paragraphText = paragraph.Text;

                    if (string.IsNullOrWhiteSpace(paragraphText)) continue;
                    textBuilder.AppendLine(paragraphText);
                        
                    // Simple heading detection for .doc files
                    if (IsLikelyHeading(paragraphText))
                    {
                        sections.Add(new WordSection
                        {
                            Title = paragraphText.Trim(),
                            Level = 1,
                            Content = paragraphText.Trim()
                        });
                    }
                }
                
                // Extract tables if present
                var tables = new List<WordTable>();
                var tableBuilder = new StringBuilder();
                var inTable = false;

                for (var i = 0; i < range.NumParagraphs; i++)
                {
                    NPOI.HWPF.UserModel.Paragraph? paragraph = range.GetParagraph(i);
    
                    if (paragraph.IsInTable())
                    {
                        if (!inTable)
                        {
                            // Starting a new table
                            inTable = true;
                            tableBuilder.Clear();
                            tableBuilder.AppendLine("[TABLE]");
                        }
        
                        // Add table row content
                        tableBuilder.AppendLine(paragraph.Text);
                    }
                    else if (inTable)
                    {
                        // End of table
                        inTable = false;
                        tableBuilder.AppendLine("[/TABLE]");
        
                        tables.Add(new WordTable { Content = tableBuilder.ToString().Trim() });
                        tableBuilder.Clear();
                    }
                }

                // Handle table that ends at document end
                if (inTable && tableBuilder.Length > 0)
                {
                    tableBuilder.AppendLine("[/TABLE]");
                    tables.Add(new WordTable { Content = tableBuilder.ToString().Trim() });
                }
                
                wordContent.PlainText = textBuilder.ToString();
                wordContent.Sections = sections;
                wordContent.Tables = tables;
            }
            catch (Exception ex)
            {
                textBuilder.AppendLine($"Error reading .doc file: {ex.Message}");
                wordContent.PlainText = textBuilder.ToString();
            }
            
            return wordContent;
        });
    }
    
    private static bool IsLikelyHeading(string text)
    {
        string trimmed = text.Trim();
        return trimmed.Length < 100 && 
               (trimmed.Contains("CHAPTER") || trimmed.Contains("SECTION") || 
                trimmed.StartsWith("1.") || trimmed.StartsWith("2.") || 
                trimmed.All(c => char.IsUpper(c) || char.IsWhiteSpace(c) || char.IsDigit(c) || c == '.'));
    }

    private static void ProcessParagraph(Paragraph paragraph, StringBuilder textBuilder, 
        List<WordSection> sections, ref WordSection currentSection, ref StringBuilder currentSectionContent)
    {
        string paragraphText = GetParagraphText(paragraph);
        if (string.IsNullOrWhiteSpace(paragraphText)) return;
        
        // Check if this is a heading
        string? styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        bool isHeading = IsHeadingStyle(styleId, paragraphText);
        
        if (isHeading)
        {
            // Save previous section if it has content
            if (currentSectionContent.Length > 0)
            {
                currentSection.Content = currentSectionContent.ToString();
                sections.Add(currentSection);
            }
            
            // Start new section
            currentSection = new WordSection
            {
                Title = paragraphText.Trim(),
                Level = GetHeadingLevel(styleId),
            };
            currentSectionContent = new StringBuilder();
            
            textBuilder.AppendLine($"\n=== {paragraphText.Trim()} ===");
        }
        else
        {
            // Regular paragraph
            textBuilder.AppendLine(paragraphText);
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

    private static void ProcessTable(Table table, StringBuilder textBuilder, List<WordTable> tables)
    {
        var tableContent = new StringBuilder();
        textBuilder.AppendLine("\n[TABLE]");
        
        foreach (TableRow row in table.Elements<TableRow>())
        {
            var rowText = new List<string>();
            
            foreach (TableCell cell in row.Elements<TableCell>())
            {
                var cellText = new StringBuilder();
                foreach (Paragraph paragraph in cell.Elements<Paragraph>())
                {
                    cellText.Append(GetParagraphText(paragraph).Trim());
                }
                rowText.Add(cellText.ToString());
            }
            
            string rowString = string.Join(" | ", rowText);
            textBuilder.AppendLine(rowString);
            tableContent.AppendLine(rowString);
        }
        
        textBuilder.AppendLine("[/TABLE]\n");
        
        var wordTable = new WordTable 
        { 
            Content = tableContent.ToString().Trim()
        };
        tables.Add(wordTable);
    }

    private static void ProcessComments(WordprocessingCommentsPart commentsPart, List<WordComment> comments)
    {
        foreach (Comment comment in commentsPart.Comments.Elements<Comment>())
        {
            var commentText = new StringBuilder();
            foreach (Paragraph paragraph in comment.Elements<Paragraph>())
            {
                commentText.AppendLine(GetParagraphText(paragraph));
            }
            
            comments.Add(new WordComment
            {
                Id = comment.Id?.Value ?? "",
                Author = comment.Author?.Value ?? "",
                Content = commentText.ToString().Trim()
            });
        }
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
}