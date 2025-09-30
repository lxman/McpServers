using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OfficeMcp.Models;
using OfficeMcp.Models.Results;
using OfficeMcp.Models.Excel;
using System.Collections.Concurrent;
using System.Text;

namespace OfficeMcp.Services;

public class OfficeService
{
    private readonly IExcelService _excelService;
    private readonly IWordService _wordService;
    private readonly IPowerPointService _powerPointService;
    private readonly ILogger<OfficeService> _logger;
    private readonly ConcurrentDictionary<string, OfficeDocument> _loadedDocuments;
    private readonly SemaphoreSlim _operationSemaphore;
    
    private readonly int _maxDocuments;
    private readonly int _maxFileSizeMb;
    private readonly string[] _supportedFormats;
    
    public OfficeService(IExcelService excelService, IWordService wordService, IPowerPointService powerPointService, ILogger<OfficeService> logger, IConfiguration configuration)
    {
        _excelService = excelService;
        _wordService = wordService;
        _powerPointService = powerPointService;
        _logger = logger;
        _loadedDocuments = new ConcurrentDictionary<string, OfficeDocument>();
        
        _maxDocuments = configuration.GetValue("OfficeService:MaxDocuments", 25);
        _maxFileSizeMb = configuration.GetValue("OfficeService:MaxFileSizeMB", 100);
        _supportedFormats = configuration.GetSection("OfficeService:SupportedFormats").Get<string[]>() ??
                            [".docx", ".xlsx", ".pptx", ".doc", ".xls", ".ppt"];
        
        int maxConcurrentOps = configuration.GetValue("Performance:MaxConcurrentOperations", 3);
        _operationSemaphore = new SemaphoreSlim(maxConcurrentOps, maxConcurrentOps);
    }

    public async Task<ServiceResult<LoadDocumentResult>> LoadDocumentAsync(string filePath, string? password = null)
    {
        await _operationSemaphore.WaitAsync();
        try
        {
            if (!File.Exists(filePath))
            {
                return new ServiceResult<LoadDocumentResult>
                {
                    Success = false,
                    Error = $"File not found: {filePath}"
                };
            }

            var fileInfo = new FileInfo(filePath);
            string extension = fileInfo.Extension.ToLowerInvariant();
            
            if (!_supportedFormats.Contains(extension))
            {
                return new ServiceResult<LoadDocumentResult>
                {
                    Success = false,
                    Error = $"Unsupported format: {extension}"
                };
            }

            if (fileInfo.Length > _maxFileSizeMb * 1024 * 1024)
            {
                return new ServiceResult<LoadDocumentResult>
                {
                    Success = false,
                    Error = $"File size exceeds limit: {_maxFileSizeMb}MB"
                };
            }

            if (_loadedDocuments.Count >= _maxDocuments)
            {
                return new ServiceResult<LoadDocumentResult>
                {
                    Success = false,
                    Error = $"Maximum document limit reached: {_maxDocuments}"
                };
            }

            DocumentType documentType = GetDocumentType(extension);
            var metadata = new OfficeMetadata
            {
                Title = fileInfo.Name,
                Author = "",
                Subject = "",
                Keywords = "",
                Comments = "",
                CreatedDate = fileInfo.CreationTime,
                ModifiedDate = fileInfo.LastWriteTime,
                LastSavedBy = ""
            };

            var document = new OfficeDocument
            {
                FilePath = filePath,
                FileName = fileInfo.Name,
                Type = documentType,
                FileSize = fileInfo.Length,
                LastModified = fileInfo.LastWriteTime,
                Metadata = metadata,
                IsLoaded = true
            };

            // Initialize basic content structures
            switch (documentType)
            {
                case DocumentType.Word:
                    document.WordContent = await _wordService.LoadWordContentAsync(document.FilePath, password);
                    break;
                case DocumentType.Excel:
                    document.ExcelContent = await _excelService.LoadExcelContentAsync(document.FilePath, password);
                    break;
                case DocumentType.PowerPoint:
                    document.PowerPointContent = await _powerPointService.LoadPowerPointContentAsync(document.FilePath, password);
                    break;
                case DocumentType.Unknown:
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Unsupported document type: {documentType}");
            }

            _loadedDocuments[filePath] = document;

            return new ServiceResult<LoadDocumentResult>
            {
                Success = true,
                Data = new LoadDocumentResult
                {
                    Message = $"Successfully loaded {fileInfo.Name}",
                    DocumentType = documentType,
                    Metadata = metadata
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading document: {FilePath}", filePath);
            return new ServiceResult<LoadDocumentResult>
            {
                Success = false,
                Error = $"Failed to load document: {ex.Message}"
            };
        }
        finally
        {
            _operationSemaphore.Release();
        }
    }

    public ServiceResult<LoadedDocumentsResult> GetLoadedDocuments()
    {
        try
        {
            List<DocumentInfo> documents = _loadedDocuments.Values
                .Select(doc => new DocumentInfo
                {
                    FilePath = doc.FilePath,
                    FileName = doc.FileName,
                    Type = doc.Type,
                    FileSize = doc.FileSize,
                    LastModified = doc.LastModified,
                    Title = doc.Metadata.Title,
                    Author = doc.Metadata.Author,
                    IsPasswordProtected = false // Simple implementation
                })
                .OrderBy(d => d.FileName)
                .ToList();

            return new ServiceResult<LoadedDocumentsResult>
            {
                Success = true,
                Data = new LoadedDocumentsResult
                {
                    LoadedDocuments = documents
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting loaded documents");
            return new ServiceResult<LoadedDocumentsResult>
            {
                Success = false,
                Error = $"Error retrieving documents: {ex.Message}"
            };
        }
    }

    public async Task<ServiceResult<ExtractContentResult>> ExtractAllContentAsync(string filePath)
    {
        await _operationSemaphore.WaitAsync();
        try
        {
            if (!_loadedDocuments.TryGetValue(filePath, out OfficeDocument? document))
            {
                return new ServiceResult<ExtractContentResult>
                {
                    Success = false,
                    Error = "Document not loaded"
                };
            }

            var content = new StringBuilder();
            var tables = new List<ExtractedTable>();
            var images = new List<ExtractedImage>();

            switch (document.Type)
            {
                case DocumentType.Word:
                    if (document.WordContent != null)
                        content.AppendLine(document.WordContent.PlainText);
                    break;
                case DocumentType.Excel:
                    if (document.ExcelContent != null)
                    {
                        content.AppendLine($"=== Excel Workbook: {document.FileName} ===");
        
                        foreach (ExcelWorksheet worksheet in document.ExcelContent.Worksheets)
                        {
                            content.AppendLine($"\n=== Sheet: {worksheet.Name} ===");
                            content.AppendLine($"Rows: {worksheet.RowCount}, Columns: {worksheet.ColumnCount}");
            
                            // Group cells by row for readable output
                            IEnumerable<IGrouping<int, ExcelCell>> cellsByRow = worksheet.Cells
                                .Where(c => c.Value != null && !string.IsNullOrWhiteSpace(c.Value.ToString()))
                                .GroupBy(c => c.Row)
                                .OrderBy(g => g.Key)
                                .Take(50); // Limit to prevent huge output
            
                            foreach (IGrouping<int, ExcelCell> rowGroup in cellsByRow)
                            {
                                List<ExcelCell> rowCells = rowGroup.OrderBy(c => c.Column).ToList();
                                IEnumerable<string> rowValues = rowCells.Select(c => 
                                {
                                    string value = c.Value?.ToString() ?? "";
                                    if (!string.IsNullOrEmpty(c.Formula))
                                        value += $" [{c.Formula}]";
                                    return $"{c.Address}:{value}";
                                });
                
                                content.AppendLine($"Row {rowGroup.Key}: {string.Join(" | ", rowValues)}");
                            }
                        }
                    }
                    break;
                case DocumentType.PowerPoint:
                    content.AppendLine($"PowerPoint presentation: {document.FileName}");
                    break;
                case DocumentType.Unknown:
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Unknown document type: {document.Type}");
            }

            var plainText = content.ToString();
            return new ServiceResult<ExtractContentResult>
            {
                Success = true,
                Data = new ExtractContentResult
                {
                    FilePath = filePath,
                    DocumentType = document.Type,
                    PlainText = plainText,
                    WordCount = plainText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length,
                    Tables = tables,
                    Images = images
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting content: {FilePath}", filePath);
            return new ServiceResult<ExtractContentResult>
            {
                Success = false,
                Error = $"Error extracting content: {ex.Message}"
            };
        }
        finally
        {
            _operationSemaphore.Release();
        }
    }

    public async Task<ServiceResult<SearchDocumentResult>> SearchInDocumentAsync(string filePath, string searchTerm, 
        bool fuzzySearch = false, int maxResults = 50)
    {
        await _operationSemaphore.WaitAsync();
        try
        {
            if (!_loadedDocuments.TryGetValue(filePath, out OfficeDocument? document))
            {
                return new ServiceResult<SearchDocumentResult>
                {
                    Success = false,
                    Error = "Document not loaded"
                };
            }

            var results = new List<OfficeSearchResult>();

            // Simple search implementation
            string content = document.Type switch
            {
                DocumentType.Word => document.WordContent?.PlainText ?? "",
                DocumentType.Excel => $"Excel workbook: {document.FileName}",
                DocumentType.PowerPoint => $"PowerPoint presentation: {document.FileName}",
                _ => ""
            };

            if (content.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new OfficeSearchResult
                {
                    FilePath = filePath,
                    DocumentType = document.Type,
                    Location = "Document Content",
                    MatchedText = searchTerm,
                    Context = GetContext(content, searchTerm, 150),
                    RelevanceScore = 1.0
                });
            }

            return new ServiceResult<SearchDocumentResult>
            {
                Success = true,
                Data = new SearchDocumentResult
                {
                    FilePath = filePath,
                    SearchTerm = searchTerm,
                    Results = results
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching document: {FilePath}", filePath);
            return new ServiceResult<SearchDocumentResult>
            {
                Success = false,
                Error = $"Error searching document: {ex.Message}"
            };
        }
        finally
        {
            _operationSemaphore.Release();
        }
    }

    public async Task<ServiceResult<CrossDocumentSearchResult>> SearchAcrossDocumentsAsync(string searchTerm, 
        bool fuzzySearch = false, int maxResults = 50)
    {
        try
        {
            var allResults = new List<OfficeSearchResult>();
            
            IEnumerable<Task<List<OfficeSearchResult>>> searchTasks = _loadedDocuments.Values
                .Where(doc => doc.IsLoaded)
                .Select(async doc =>
                {
                    ServiceResult<SearchDocumentResult> result = await SearchInDocumentAsync(doc.FilePath, searchTerm, fuzzySearch, maxResults);
                    return result.Success ? result.Data!.Results : [];
                });

            List<OfficeSearchResult>[] searchResults = await Task.WhenAll(searchTasks);
            
            foreach (List<OfficeSearchResult> results in searchResults)
            {
                allResults.AddRange(results);
            }

            List<OfficeSearchResult> sortedResults = allResults
                .OrderByDescending(r => r.RelevanceScore)
                .Take(maxResults)
                .ToList();

            return new ServiceResult<CrossDocumentSearchResult>
            {
                Success = true,
                Data = new CrossDocumentSearchResult
                {
                    SearchTerm = searchTerm,
                    TotalResults = allResults.Count,
                    Results = sortedResults
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching across documents");
            return new ServiceResult<CrossDocumentSearchResult>
            {
                Success = false,
                Error = $"Error searching across documents: {ex.Message}"
            };
        }
    }

    public async Task<ServiceResult<DocumentAnalysisResult>> AnalyzeDocumentAsync(string filePath)
    {
        await _operationSemaphore.WaitAsync();
        try
        {
            if (!_loadedDocuments.TryGetValue(filePath, out OfficeDocument? document))
            {
                return new ServiceResult<DocumentAnalysisResult>
                {
                    Success = false,
                    Error = "Document not loaded"
                };
            }

            var analysis = new DocumentAnalysisResult
            {
                FilePath = filePath,
                DocumentType = document.Type,
                FileSize = document.FileSize,
                LastModified = document.LastModified,
                Statistics = new Dictionary<string, object>()
            };

            switch (document.Type)
            {
                case DocumentType.Word:
                    analysis.Statistics["PlainTextLength"] = document.WordContent?.PlainText?.Length ?? 0;
                    analysis.Statistics["SectionCount"] = document.WordContent?.Sections?.Count ?? 0;
                    analysis.Statistics["TableCount"] = document.WordContent?.Tables?.Count ?? 0;
                    analysis.Statistics["CommentCount"] = document.WordContent?.Comments?.Count ?? 0;
                    break;
                case DocumentType.Excel:
                    analysis.Statistics["WorksheetCount"] = document.ExcelContent?.Worksheets?.Count ?? 0;
                    analysis.Statistics["ChartCount"] = document.ExcelContent?.Charts?.Count ?? 0;
                    analysis.Statistics["TableCount"] = document.ExcelContent?.Tables?.Count ?? 0;
                    break;
                case DocumentType.PowerPoint:
                    analysis.Statistics["SlideCount"] = document.PowerPointContent?.Slides?.Count ?? 0;
                    break;
                case DocumentType.Unknown:
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Unknown document type: {document.Type}");
            }

            return new ServiceResult<DocumentAnalysisResult>
            {
                Success = true,
                Data = analysis
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing document: {FilePath}", filePath);
            return new ServiceResult<DocumentAnalysisResult>
            {
                Success = false,
                Error = $"Error analyzing document: {ex.Message}"
            };
        }
        finally
        {
            _operationSemaphore.Release();
        }
    }

    public ServiceResult<SimpleOperationResult> UnloadDocument(string filePath)
    {
        try
        {
            if (_loadedDocuments.TryRemove(filePath, out _))
            {
                return new ServiceResult<SimpleOperationResult>
                {
                    Success = true,
                    Data = new SimpleOperationResult { Message = "true" }
                };
            }
            
            return new ServiceResult<SimpleOperationResult>
            {
                Success = false,
                Error = "Document not found or already unloaded"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unloading document: {FilePath}", filePath);
            return new ServiceResult<SimpleOperationResult>
            {
                Success = false,
                Error = $"Error unloading document: {ex.Message}"
            };
        }
    }

    public ServiceResult<SimpleOperationResult> ClearAllDocuments()
    {
        try
        {
            _loadedDocuments.Clear();
            GC.Collect();
            
            return new ServiceResult<SimpleOperationResult>
            {
                Success = true,
                Data = new SimpleOperationResult { Message = "true" }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing all documents");
            return new ServiceResult<SimpleOperationResult>
            {
                Success = false,
                Error = $"Error clearing documents: {ex.Message}"
            };
        }
    }

    public ServiceResult<ServiceStatusInfo> GetServiceStatus()
    {
        try
        {
            long memoryUsage = GC.GetTotalMemory(false);
            
            return new ServiceResult<ServiceStatusInfo>
            {
                Success = true,
                Data = new ServiceStatusInfo
                {
                    IsHealthy = true,
                    LoadedDocumentCount = _loadedDocuments.Count,
                    MaxDocuments = _maxDocuments,
                    MemoryUsage = new MemoryUsageInfo
                    {
                        TotalMemoryMb = memoryUsage / (1024.0 * 1024.0),
                        DocumentCacheSize = _loadedDocuments.Count
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting service status");
            return new ServiceResult<ServiceStatusInfo>
            {
                Success = false,
                Error = $"Error getting service status: {ex.Message}"
            };
        }
    }
    
    private static DocumentType GetDocumentType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".docx" or ".doc" => DocumentType.Word,
            ".xlsx" or ".xls" => DocumentType.Excel,
            ".pptx" or ".ppt" => DocumentType.PowerPoint,
            _ => DocumentType.Unknown
        };
    }

    private static string GetContext(string text, string searchTerm, int contextSize)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(searchTerm))
            return text;

        int index = text.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase);
        if (index == -1) 
            return text.Length <= contextSize ? text : text[..contextSize];

        int start = Math.Max(0, index - contextSize / 2);
        int length = Math.Min(contextSize, text.Length - start);
        
        return text.Substring(start, length);
    }
}
