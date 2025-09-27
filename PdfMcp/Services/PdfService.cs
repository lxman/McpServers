using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using PdfMcp.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using FuzzySharp;
using FuzzySharp.Extractor;
using PdfDocument = PdfMcp.Models.PdfDocument;
using PdfPage = PdfMcp.Models.PdfPage;

namespace PdfMcp.Services;

public class PdfService(ILogger<PdfService> logger)
{
    private readonly Dictionary<string, PdfDocument> _loadedDocuments = new();

    #region Core Document Operations

    public async Task<ServiceResult<LoadPdfResult>> LoadPdfAsync(string filePath, string? password = null)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return new ServiceResult<LoadPdfResult> { Success = false, Error = "File not found" };
            }

            var pdfDoc = new PdfDocument
            {
                FilePath = filePath,
                FileName = Path.GetFileName(filePath),
                FileSize = new FileInfo(filePath).Length,
                LastModified = File.GetLastWriteTime(filePath)
            };

            await ExtractMetadataAsync(pdfDoc, password);
            await ExtractContentAsync(pdfDoc, password);

            if (!string.IsNullOrEmpty(pdfDoc.LoadError))
            {
                return new ServiceResult<LoadPdfResult> { Success = false, Error = pdfDoc.LoadError };
            }

            pdfDoc.IsLoaded = true;
            _loadedDocuments[filePath] = pdfDoc;

            return new ServiceResult<LoadPdfResult>
            {
                Success = true,
                Data = new LoadPdfResult
                {
                    Message = $"PDF loaded successfully: {pdfDoc.FileName}",
                    Metadata = pdfDoc.Metadata,
                    PageCount = pdfDoc.Pages.Count
                }
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading PDF: {FilePath}", filePath);
            return new ServiceResult<LoadPdfResult> { Success = false, Error = ex.Message };
        }
    }

    public ServiceResult<LoadedDocumentsResult> GetLoadedDocuments()
    {
        List<DocumentInfo> docs = _loadedDocuments.Values.Select(doc => new DocumentInfo
        {
            FileName = doc.FileName,
            FilePath = doc.FilePath,
            FileSize = doc.FileSize,
            PageCount = doc.Metadata.PageCount,
            Title = doc.Metadata.Title,
            Author = doc.Metadata.Author,
            LastModified = doc.LastModified,
            IsEncrypted = doc.Metadata.IsEncrypted
        }).ToList();

        return new ServiceResult<LoadedDocumentsResult>
        {
            Success = true,
            Data = new LoadedDocumentsResult { LoadedDocuments = docs }
        };
    }

    public ServiceResult<DocumentInfoResult> GetDocumentInfo(string filePath)
    {
        if (!_loadedDocuments.TryGetValue(filePath, out PdfDocument? doc))
        {
            return new ServiceResult<DocumentInfoResult> { Success = false, Error = "Document not loaded" };
        }

        return new ServiceResult<DocumentInfoResult>
        {
            Success = true,
            Data = new DocumentInfoResult
            {
                FileName = doc.FileName,
                FilePath = doc.FilePath,
                FileSize = doc.FileSize,
                Metadata = doc.Metadata,
                PageCount = doc.Pages.Count,
                HasImages = doc.Pages.Any(p => p.Images.Count != 0),
                HasAnnotations = doc.Pages.Any(p => p.Annotations.Count != 0),
                HasLinks = doc.Pages.Any(p => p.Links.Count != 0)
            }
        };
    }

    public ServiceResult<PageContentResult> GetPageContent(string filePath, int pageNumber)
    {
        if (!_loadedDocuments.TryGetValue(filePath, out PdfDocument? doc))
        {
            return new ServiceResult<PageContentResult> { Success = false, Error = "Document not loaded" };
        }

        PdfPage? page = doc.Pages.FirstOrDefault(p => p.PageNumber == pageNumber);
        if (page == null)
        {
            return new ServiceResult<PageContentResult> { Success = false, Error = "Page not found" };
        }

        return new ServiceResult<PageContentResult>
        {
            Success = true,
            Data = new PageContentResult
            {
                PageNumber = page.PageNumber,
                Text = page.Text,
                Dimensions = new PageDimensions { Width = page.Width, Height = page.Height },
                Rotation = page.Rotation,
                Images = page.Images,
                Annotations = page.Annotations,
                Links = page.Links
            }
        };
    }

    #endregion

    #region Search Operations

    public ServiceResult<SearchInDocumentResult> SearchInDocument(string filePath, string searchTerm, bool fuzzySearch = false, int maxResults = 50)
    {
        if (!_loadedDocuments.TryGetValue(filePath, out PdfDocument? doc))
        {
            return new ServiceResult<SearchInDocumentResult> { Success = false, Error = "Document not loaded" };
        }

        var results = new List<SearchResult>();

        foreach (PdfPage page in doc.Pages)
        {
            if (fuzzySearch)
            {
                List<string> sentences = SplitIntoSentences(page.Text);
                IEnumerable<ExtractedResult<string>> matches = Process.ExtractTop(searchTerm, sentences, limit: 10)
                    .Where(match => match.Score >= 60);

                results.AddRange(from match in matches
                    let startIndex = page.Text.IndexOf(match.Value, StringComparison.OrdinalIgnoreCase)
                    where startIndex >= 0
                    select new SearchResult
                    {
                        FilePath = filePath,
                        PageNumber = page.PageNumber,
                        MatchedText = match.Value,
                        Context = GetContext(page.Text, startIndex, match.Value.Length),
                        StartIndex = startIndex,
                        EndIndex = startIndex + match.Value.Length,
                        RelevanceScore = match.Score / 100.0
                    });
            }
            else
            {
                var regex = new Regex(Regex.Escape(searchTerm), RegexOptions.IgnoreCase);
                MatchCollection matches = regex.Matches(page.Text);

                foreach (Match match in matches)
                {
                    results.Add(new SearchResult
                    {
                        FilePath = filePath,
                        PageNumber = page.PageNumber,
                        MatchedText = match.Value,
                        Context = GetContext(page.Text, match.Index, match.Length),
                        StartIndex = match.Index,
                        EndIndex = match.Index + match.Length,
                        RelevanceScore = 1.0
                    });
                }
            }

            if (results.Count >= maxResults) break;
        }

        return new ServiceResult<SearchInDocumentResult>
        {
            Success = true,
            Data = new SearchInDocumentResult
            {
                SearchTerm = searchTerm,
                TotalResults = results.Count,
                Results = results.Take(maxResults).ToList()
            }
        };
    }

    public ServiceResult<CrossDocumentSearchResult> SearchAcrossAllDocuments(string searchTerm, bool fuzzySearch = false, int maxResultsPerDocument = 10)
    {
        ServiceResult<LoadedDocumentsResult> loadedDocsResult = GetLoadedDocuments();
        if (!loadedDocsResult.Success || loadedDocsResult.Data!.LoadedDocuments.Count == 0)
        {
            return new ServiceResult<CrossDocumentSearchResult> { Success = false, Error = "No documents currently loaded" };
        }

        var allResults = new List<CrossDocumentSearchMatch>();

        foreach (DocumentInfo docInfo in loadedDocsResult.Data.LoadedDocuments)
        {
            ServiceResult<SearchInDocumentResult> searchResult = SearchInDocument(docInfo.FilePath, searchTerm, fuzzySearch, maxResultsPerDocument);
            
            if (searchResult is { Success: true, Data: not null })
            {
                allResults.AddRange(searchResult.Data.Results.Select(result => new CrossDocumentSearchMatch
                {
                    FileName = Path.GetFileName(result.FilePath),
                    FilePath = result.FilePath,
                    PageNumber = result.PageNumber,
                    MatchedText = result.MatchedText,
                    Context = result.Context,
                    RelevanceScore = result.RelevanceScore
                }));
            }
        }

        // Sort by relevance score
        allResults = allResults.OrderByDescending(r => r.RelevanceScore).ToList();

        return new ServiceResult<CrossDocumentSearchResult>
        {
            Success = true,
            Data = new CrossDocumentSearchResult
            {
                SearchTerm = searchTerm,
                TotalDocuments = loadedDocsResult.Data.LoadedDocuments.Count,
                TotalResults = allResults.Count,
                Results = allResults
            }
        };
    }

    #endregion

    #region Analysis Operations

    public ServiceResult<DocumentSummary> SummarizeDocument(string filePath, int maxLength = 500)
    {
        if (!_loadedDocuments.TryGetValue(filePath, out PdfDocument? doc))
        {
            return new ServiceResult<DocumentSummary> { Success = false, Error = "Document not loaded" };
        }

        try
        {
            string fullText = string.Join(" ", doc.Pages.Select(p => p.Text));
            DocumentSummary summary = GenerateSummary(fullText, maxLength);

            return new ServiceResult<DocumentSummary> { Success = true, Data = summary };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error summarizing document: {FilePath}", filePath);
            return new ServiceResult<DocumentSummary> { Success = false, Error = ex.Message };
        }
    }

    public ServiceResult<TextExtractionResult> ExtractAllTextFromDocument(string filePath)
    {
        ServiceResult<DocumentInfoResult> docInfo = GetDocumentInfo(filePath);
        if (!docInfo.Success || docInfo.Data == null)
        {
            return new ServiceResult<TextExtractionResult> { Success = false, Error = docInfo.Error };
        }

        try
        {
            var allText = new System.Text.StringBuilder();
            int pageCount = docInfo.Data.PageCount;

            for (var i = 1; i <= pageCount; i++)
            {
                ServiceResult<PageContentResult> pageContent = GetPageContent(filePath, i);
                if (pageContent is { Success: true, Data: not null })
                {
                    string text = pageContent.Data.Text;
                    allText.AppendLine($"=== PAGE {i} ===");
                    allText.AppendLine(text);
                    allText.AppendLine();
                }
            }

            string fullText = allText.ToString();
            int wordCount = fullText.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;

            return new ServiceResult<TextExtractionResult>
            {
                Success = true,
                Data = new TextExtractionResult
                {
                    FilePath = filePath,
                    PageCount = pageCount,
                    FullText = fullText,
                    WordCount = wordCount
                }
            };
        }
        catch (Exception ex)
        {
            return new ServiceResult<TextExtractionResult> { Success = false, Error = $"Error extracting text: {ex.Message}" };
        }
    }

    public ServiceResult<DocumentMetadataResult> GetDocumentMetadata(string filePath)
    {
        ServiceResult<DocumentInfoResult> docInfo = GetDocumentInfo(filePath);
        if (!docInfo.Success || docInfo.Data == null)
        {
            return new ServiceResult<DocumentMetadataResult> { Success = false, Error = docInfo.Error };
        }

        return new ServiceResult<DocumentMetadataResult>
        {
            Success = true,
            Data = new DocumentMetadataResult
            {
                FilePath = filePath,
                Metadata = docInfo.Data.Metadata
            }
        };
    }

    public ServiceResult<DocumentComparisonResult> CompareDocuments(string filePath1, string filePath2)
    {
        ServiceResult<DocumentInfoResult> doc1Info = GetDocumentInfo(filePath1);
        ServiceResult<DocumentInfoResult> doc2Info = GetDocumentInfo(filePath2);

        if (!doc1Info.Success || doc1Info.Data == null)
        {
            return new ServiceResult<DocumentComparisonResult> { Success = false, Error = $"Document 1 not loaded: {filePath1}" };
        }

        if (!doc2Info.Success || doc2Info.Data == null)
        {
            return new ServiceResult<DocumentComparisonResult> { Success = false, Error = $"Document 2 not loaded: {filePath2}" };
        }

        try
        {
            // Extract text from both documents
            ServiceResult<TextExtractionResult> text1Result = ExtractAllTextFromDocument(filePath1);
            ServiceResult<TextExtractionResult> text2Result = ExtractAllTextFromDocument(filePath2);

            if (!text1Result.Success || !text2Result.Success)
            {
                return new ServiceResult<DocumentComparisonResult> { Success = false, Error = "Failed to extract text for comparison" };
            }

            string text1 = text1Result.Data!.FullText;
            string text2 = text2Result.Data!.FullText;

            // Basic comparison metrics
            string[] words1 = text1.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries);
            string[] words2 = text2.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries);

            int commonWords = words1.Intersect(words2, StringComparer.OrdinalIgnoreCase).Count();
            int totalUniqueWords = words1.Union(words2, StringComparer.OrdinalIgnoreCase).Count();
            double similarity = totalUniqueWords > 0 ? (double)commonWords / totalUniqueWords : 0;

            return new ServiceResult<DocumentComparisonResult>
            {
                Success = true,
                Data = new DocumentComparisonResult
                {
                    Document1 = new DocumentComparisonInfo
                    {
                        FileName = doc1Info.Data.FileName,
                        PageCount = doc1Info.Data.PageCount,
                        WordCount = words1.Length,
                        FileSize = doc1Info.Data.FileSize
                    },
                    Document2 = new DocumentComparisonInfo
                    {
                        FileName = doc2Info.Data.FileName,
                        PageCount = doc2Info.Data.PageCount,
                        WordCount = words2.Length,
                        FileSize = doc2Info.Data.FileSize
                    },
                    Comparison = new ComparisonMetrics
                    {
                        TextSimilarity = Math.Round(similarity * 100, 2),
                        CommonWords = commonWords,
                        UniqueToDoc1 = words1.Length - commonWords,
                        UniqueToDoc2 = words2.Length - commonWords,
                        PageDifference = Math.Abs(doc1Info.Data.PageCount - doc2Info.Data.PageCount),
                        SizeDifference = Math.Abs(doc1Info.Data.FileSize - doc2Info.Data.FileSize)
                    }
                }
            };
        }
        catch (Exception ex)
        {
            return new ServiceResult<DocumentComparisonResult> { Success = false, Error = $"Error comparing documents: {ex.Message}" };
        }
    }

    public ServiceResult<DocumentStructureAnalysis> AnalyzeDocumentStructure(string filePath)
    {
        ServiceResult<DocumentInfoResult> docInfo = GetDocumentInfo(filePath);
        if (!docInfo.Success || docInfo.Data == null)
        {
            return new ServiceResult<DocumentStructureAnalysis> { Success = false, Error = docInfo.Error };
        }

        try
        {
            int pageCount = docInfo.Data.PageCount;
            var pages = new List<PageAnalysis>();
            var totalWords = 0;
            var totalCharacters = 0;
            var totalImages = 0;
            var totalAnnotations = 0;
            var totalLinks = 0;

            for (var i = 1; i <= pageCount; i++)
            {
                ServiceResult<PageContentResult> pageContent = GetPageContent(filePath, i);
                if (pageContent is { Success: true, Data: not null })
                {
                    string text = pageContent.Data.Text;
                    string[] words = text.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries);

                    totalWords += words.Length;
                    totalCharacters += text.Length;
                    totalImages += pageContent.Data.Images.Count;
                    totalAnnotations += pageContent.Data.Annotations.Count;
                    totalLinks += pageContent.Data.Links.Count;

                    pages.Add(new PageAnalysis
                    {
                        PageNumber = i,
                        WordCount = words.Length,
                        CharacterCount = text.Length,
                        ImageCount = pageContent.Data.Images.Count,
                        AnnotationCount = pageContent.Data.Annotations.Count,
                        LinkCount = pageContent.Data.Links.Count,
                        Dimensions = pageContent.Data.Dimensions,
                        Rotation = pageContent.Data.Rotation,
                        HasContent = words.Length > 0
                    });
                }
            }

            return new ServiceResult<DocumentStructureAnalysis>
            {
                Success = true,
                Data = new DocumentStructureAnalysis
                {
                    FilePath = filePath,
                    FileName = docInfo.Data.FileName,
                    PageCount = pageCount,
                    TotalWords = totalWords,
                    TotalCharacters = totalCharacters,
                    AverageWordsPerPage = pageCount > 0 ? Math.Round((double)totalWords / pageCount, 2) : 0,
                    HasImages = totalImages > 0,
                    HasAnnotations = totalAnnotations > 0,
                    HasLinks = totalLinks > 0,
                    ImageCount = totalImages,
                    AnnotationCount = totalAnnotations,
                    LinkCount = totalLinks,
                    Pages = pages
                }
            };
        }
        catch (Exception ex)
        {
            return new ServiceResult<DocumentStructureAnalysis> { Success = false, Error = $"Error analyzing document structure: {ex.Message}" };
        }
    }

    #endregion

    #region Utility Operations

    public ServiceResult<ExtractImagesResult> ExtractImages(string filePath, string outputDirectory)
    {
        if (!_loadedDocuments.TryGetValue(filePath, out PdfDocument? doc))
        {
            return new ServiceResult<ExtractImagesResult> { Success = false, Error = "Document not loaded" };
        }

        try
        {
            Directory.CreateDirectory(outputDirectory);
            var extractedImages = new List<ExtractedImageInfo>();

            foreach (PdfPage page in doc.Pages)
            {
                extractedImages.AddRange(page.Images.Select((image, i) => new ExtractedImageInfo
                {
                    PageNumber = page.PageNumber,
                    ImageIndex = i,
                    FileName = $"page_{page.PageNumber}_image_{i}.png",
                    Width = image.Width,
                    Height = image.Height,
                    X = image.X,
                    Y = image.Y
                }));
            }

            return new ServiceResult<ExtractImagesResult>
            {
                Success = true,
                Data = new ExtractImagesResult
                {
                    ExtractedImages = extractedImages,
                    TotalImages = extractedImages.Count,
                    OutputDirectory = outputDirectory,
                    Note = "Image extraction feature will be implemented in next version"
                }
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error extracting images from document: {FilePath}", filePath);
            return new ServiceResult<ExtractImagesResult> { Success = false, Error = ex.Message };
        }
    }

    public ServiceResult<PdfValidationResult> ValidatePdf(string filePath)
    {
        try
        {
            var result = new PdfValidationResult { IsValid = true };
            
            if (!File.Exists(filePath))
            {
                result.IsValid = false;
                result.Errors.Add("File not found");
                return new ServiceResult<PdfValidationResult> { Success = true, Data = result };
            }

            try
            {
                using UglyToad.PdfPig.PdfDocument pdfDoc = UglyToad.PdfPig.PdfDocument.Open(filePath);
                result.PdfVersion = pdfDoc.Version.ToString(CultureInfo.InvariantCulture);
                result.IsPasswordProtected = false;

                if (pdfDoc.NumberOfPages == 0)
                {
                    result.Warnings.Add("Document has no pages");
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("password") || ex.Message.Contains("encrypted"))
                {
                    result.IsPasswordProtected = true;
                    result.Warnings.Add("Document is password protected");
                }
                else
                {
                    result.IsValid = false;
                    result.Errors.Add($"PDF validation failed: {ex.Message}");
                }
            }

            return new ServiceResult<PdfValidationResult> { Success = true, Data = result };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error validating PDF: {FilePath}", filePath);
            return new ServiceResult<PdfValidationResult> 
            { 
                Success = true, 
                Data = new PdfValidationResult 
                { 
                    IsValid = false, 
                    Errors = { ex.Message },
                    IsCorrupted = true 
                }
            };
        }
    }

    public ServiceResult<ServiceStatusInfo> GetServiceStatus()
    {
        ServiceResult<LoadedDocumentsResult> loadedDocsResult = GetLoadedDocuments();
        if (!loadedDocsResult.Success || loadedDocsResult.Data == null)
        {
            return new ServiceResult<ServiceStatusInfo> { Success = false, Error = "Failed to get loaded documents" };
        }

        long totalSize = loadedDocsResult.Data.LoadedDocuments.Sum(doc => doc.FileSize);
        int totalPages = loadedDocsResult.Data.LoadedDocuments.Sum(doc => doc.PageCount);

        return new ServiceResult<ServiceStatusInfo>
        {
            Success = true,
            Data = new ServiceStatusInfo
            {
                LoadedDocuments = loadedDocsResult.Data.LoadedDocuments.Count,
                TotalFileSize = totalSize,
                TotalPages = totalPages,
                MemoryUsage = new MemoryUsageInfo
                {
                    WorkingSet = GC.GetTotalMemory(false),
                    Gen0Collections = GC.CollectionCount(0),
                    Gen1Collections = GC.CollectionCount(1),
                    Gen2Collections = GC.CollectionCount(2)
                },
                Uptime = DateTime.Now
            }
        };
    }

    public ServiceResult<SimpleOperationResult> UnloadDocument(string filePath)
    {
        if (_loadedDocuments.Remove(filePath))
        {
            return new ServiceResult<SimpleOperationResult> 
            { 
                Success = true, 
                Data = new SimpleOperationResult { Message = "Document unloaded successfully" }
            };
        }
        return new ServiceResult<SimpleOperationResult> 
        { 
            Success = false, 
            Error = "Document not found in memory" 
        };
    }

    public ServiceResult<SimpleOperationResult> ClearAllDocuments()
    {
        int count = _loadedDocuments.Count;
        _loadedDocuments.Clear();
        return new ServiceResult<SimpleOperationResult> 
        { 
            Success = true, 
            Data = new SimpleOperationResult { Message = $"Cleared {count} documents from memory" }
        };
    }

    #endregion

    #region Private Helper Methods

    private Task ExtractMetadataAsync(PdfDocument pdfDoc, string? password)
    {
        try
        {
            using UglyToad.PdfPig.PdfDocument pdfDocument = string.IsNullOrEmpty(password) 
                ? UglyToad.PdfPig.PdfDocument.Open(pdfDoc.FilePath)
                : UglyToad.PdfPig.PdfDocument.Open(pdfDoc.FilePath, new ParsingOptions { Password = password });

            DocumentInformation info = pdfDocument.Information;
            pdfDoc.Metadata = new PdfMetadata
            {
                Title = info.Title ?? "",
                Author = info.Author ?? "",
                Subject = info.Subject ?? "",
                Keywords = info.Keywords ?? "",
                Creator = info.Creator ?? "",
                Producer = info.Producer ?? "",
                CreationDate = ParsePdfDate(info.CreationDate),
                ModificationDate = ParsePdfDate(info.ModifiedDate),
                Version = pdfDocument.Version.ToString(CultureInfo.InvariantCulture),
                PageCount = pdfDocument.NumberOfPages,
                IsEncrypted = !string.IsNullOrEmpty(password)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error extracting metadata from PDF: {FilePath}", pdfDoc.FilePath);
            pdfDoc.LoadError = ex.Message;
        }

        return Task.CompletedTask;
    }

    private static DateTime ParsePdfDate(string? pdfDate)
    {
        if (string.IsNullOrEmpty(pdfDate))
            return new DateTime(1970, 1, 1);

        try
        {
            // PDF dates can be in format: D:YYYYMMDDHHmmSSOHH'mm or D:YYYYMMDDHHMMSS
            if (pdfDate.StartsWith("D:"))
            {
                string dateStr = pdfDate[2..]; // Remove "D:" prefix
                
                // Handle timezone offset (remove +08'00' or similar)
                int timezoneIndex = dateStr.IndexOfAny(['+', '-']);
                if (timezoneIndex > 0)
                {
                    dateStr = dateStr[..timezoneIndex];
                }
                
                // Pad the date string to ensure we have all components
                dateStr = dateStr.PadRight(14, '0');
                
                // Parse: YYYYMMDDHHMMSS
                if (dateStr.Length >= 14 && 
                    int.TryParse(dateStr[..4], out int year) &&
                    int.TryParse(dateStr[4..6], out int month) &&
                    int.TryParse(dateStr[6..8], out int day) &&
                    int.TryParse(dateStr[8..10], out int hour) &&
                    int.TryParse(dateStr[10..12], out int minute) &&
                    int.TryParse(dateStr[12..14], out int second))
                {
                    // Validate ranges
                    if (year is >= 1900 and <= 9999 &&
                        month is >= 1 and <= 12 &&
                        day is >= 1 and <= 31 &&
                        hour is >= 0 and <= 23 &&
                        minute is >= 0 and <= 59 &&
                        second is >= 0 and <= 59)
                    {
                        try
                        {
                            return new DateTime(year, month, day, hour, minute, second);
                        }
                        catch
                        {
                            // Invalid date combination (e.g., Feb 30)
                            return new DateTime(1970, 1, 1);
                        }
                    }
                }
            }
            
            // Fallback: try standard DateTime parsing
            if (DateTime.TryParse(pdfDate, out DateTime result))
                return result;
                
            return new DateTime(1970, 1, 1);
        }
        catch
        {
            return new DateTime(1970, 1, 1);
        }
    }

    private Task ExtractContentAsync(PdfDocument pdfDoc, string? password)
    {
        try
        {
            using UglyToad.PdfPig.PdfDocument pdfDocument = string.IsNullOrEmpty(password) 
                ? UglyToad.PdfPig.PdfDocument.Open(pdfDoc.FilePath)
                : UglyToad.PdfPig.PdfDocument.Open(pdfDoc.FilePath, new ParsingOptions { Password = password });

            foreach (Page page in pdfDocument.GetPages())
            {
                var pdfPage = new PdfPage
                {
                    PageNumber = page.Number,
                    Text = ContentOrderTextExtractor.GetText(page),
                    Width = page.Width,
                    Height = page.Height,
                    Rotation = page.Rotation.Value
                };

                IEnumerable<IPdfImage> images = page.GetImages().ToList();
                for (var i = 0; i < images.Count(); i++)
                {
                    IPdfImage image = images.ElementAt(i);
                    pdfPage.Images.Add(new PdfImage
                    {
                        ImageId = i,
                        Name = $"image_{i}",
                        Width = (int)image.Bounds.Width,
                        Height = (int)image.Bounds.Height,
                        X = image.Bounds.Left,
                        Y = image.Bounds.Bottom,
                        SizeInBytes = image.RawBytes.Length
                    });
                }

                pdfDoc.Pages.Add(pdfPage);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error extracting content from PDF: {FilePath}", pdfDoc.FilePath);
            pdfDoc.LoadError = ex.Message;
        }

        return Task.CompletedTask;
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

    private static string GetContext(string text, int startIndex, int matchLength, int contextSize = 100)
    {
        int contextStart = Math.Max(0, startIndex - contextSize);
        int contextEnd = Math.Min(text.Length, startIndex + matchLength + contextSize);
        return text.Substring(contextStart, contextEnd - contextStart);
    }

    private static List<string> SplitIntoSentences(string text)
    {
        return text.Split(['.', '!', '?'], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 10)
            .ToList();
    }

    #endregion
}