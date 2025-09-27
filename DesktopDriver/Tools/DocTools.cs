using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DesktopDriver.Services;
using DesktopDriver.Services.DocumentSearching;
using DesktopDriver.Services.DocumentSearching.Models;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
// ReSharper disable InconsistentNaming

namespace DesktopDriver.Tools;

[McpServerToolType]
public class DocTools(
    SecurityManager securityManager,
    AuditLogger auditLogger,
    PasswordManager passwordManager,
    DocumentProcessor documentProcessor,
    DocumentIndexer documentIndexer,
    OcrService ocrService,
    ILogger<DocTools> logger)
{
    #region Password Management

    [McpServerTool]
    [Description("Register a password for documents matching a specific pattern")]
    public Task<string> RegisterPasswordPattern(
        [Description("File pattern (e.g., '**/PAY.gov/**', '**/TADERA*')")] string pattern,
        [Description("Password for documents matching this pattern")] string password)
    {
        try
        {
            passwordManager.RegisterPasswordPattern(pattern, password);
            auditLogger.LogPasswordOperation("RegisterPattern", pattern, true);
            return Task.FromResult($"Password pattern registered successfully: {pattern}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to register password pattern: {Pattern}", pattern);
            auditLogger.LogPasswordOperation("RegisterPattern", pattern, false, ex.Message);
            return Task.FromResult($"Failed to register password pattern: {ex.Message}");
        }
    }

    [McpServerTool]
    [Description("Register a password for a specific file")]
    public Task<string> RegisterSpecificPassword(
        [Description("Full path to the file - must be canonical")] string filePath,
        [Description("Password for this specific file")] string password)
    {
        try
        {
            if (!securityManager.IsDirectoryAllowed(Path.GetDirectoryName(filePath)!))
            {
                var error = $"Access denied to directory: {Path.GetDirectoryName(filePath)}";
                auditLogger.LogPasswordOperation("RegisterSpecific", filePath, false, error);
                return Task.FromResult(error);
            }

            passwordManager.RegisterSpecificPassword(filePath, password);
            auditLogger.LogPasswordOperation("RegisterSpecific", filePath, true);
            return Task.FromResult($"Password registered for file: {Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to register specific password: {FilePath}", filePath);
            auditLogger.LogPasswordOperation("RegisterSpecific", filePath, false, ex.Message);
            return Task.FromResult($"Failed to register password: {ex.Message}");
        }
    }

    [McpServerTool]
    [Description("Automatically detect and register passwords from password files (*.txt, *.pwd)")]
    public async Task<string> AutoDetectPasswords(
        [Description("Root directory to search for password files - must be canonical")] string rootPath)
    {
        try
        {
            string fullPath = Path.GetFullPath(rootPath);
            if (!securityManager.IsDirectoryAllowed(fullPath))
            {
                var error = $"Access denied to directory: {fullPath}";
                auditLogger.LogPasswordOperation("AutoDetect", fullPath, false, error);
                return error;
            }

            await passwordManager.AutoDetectPasswordFiles(fullPath);
            auditLogger.LogPasswordOperation("AutoDetect", fullPath, true);
            return $"Password auto-detection completed for: {rootPath}";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to auto-detect passwords: {RootPath}", rootPath);
            auditLogger.LogPasswordOperation("AutoDetect", rootPath, false, ex.Message);
            return $"Failed to auto-detect passwords: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Register multiple password patterns from JSON")]
    public Task<string> BulkRegisterPasswords(
        [Description("JSON object with pattern-password mappings")] string passwordMapJson)
    {
        try
        {
            var passwordMap = JsonSerializer.Deserialize<Dictionary<string, string>>(passwordMapJson);
            if (passwordMap == null)
            {
                return Task.FromResult("Invalid JSON format for password map");
            }

            var registered = 0;
            var failed = 0;

            foreach ((string pattern, string password) in passwordMap)
            {
                try
                {
                    passwordManager.RegisterPasswordPattern(pattern, password);
                    registered++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to register pattern: {Pattern}", pattern);
                    failed++;
                }
            }

            var result = $"Bulk password registration completed: {registered} successful, {failed} failed";
            auditLogger.LogPasswordOperation("BulkRegister", passwordMapJson.Substring(0, Math.Min(100, passwordMapJson.Length)), registered > 0);
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to bulk register passwords");
            auditLogger.LogPasswordOperation("BulkRegister", "JSON", false, ex.Message);
            return Task.FromResult($"Failed to bulk register passwords: {ex.Message}");
        }
    }

    #endregion

    #region OCR Operations

    [McpServerTool]
    [Description("Extract text from an image file using OCR (requires Tesseract OCR to be installed)")]
    public async Task<string> ExtractTextFromImage(
        [Description("Path to the image file - must be canonical")] string imagePath)
    {
        try
        {
            string fullPath = Path.GetFullPath(imagePath);
            if (!securityManager.IsDirectoryAllowed(Path.GetDirectoryName(fullPath)!))
            {
                var error = $"Access denied to directory: {Path.GetDirectoryName(fullPath)}";
                auditLogger.LogFileOperation("OCR_ExtractFromImage", fullPath, false, error);
                return error;
            }

            if (!File.Exists(fullPath))
            {
                var error = $"File not found: {fullPath}";
                auditLogger.LogFileOperation("OCR_ExtractFromImage", fullPath, false, error);
                return error;
            }

            if (!IsImageFile(fullPath))
            {
                var error = $"File is not a supported image format: {fullPath}";
                auditLogger.LogFileOperation("OCR_ExtractFromImage", fullPath, false, error);
                return error;
            }

            if (!ocrService.IsAvailable)
            {
                var warning = "OCR service is not available. Please ensure Tesseract is installed and tessdata is properly configured.";
                auditLogger.LogFileOperation("OCR_ExtractFromImage", fullPath, false, warning);
                return warning;
            }

            string extractedText = await ocrService.ExtractTextFromImage(fullPath);
            
            string result = $"OCR Text Extraction Results\n" +
            $"Image: {Path.GetFileName(fullPath)}\n" +
            $"File Size: {new FileInfo(fullPath).Length:N0} bytes\n" +
            $"Text Length: {extractedText.Length:N0} characters\n" +
            $"Word Count: {extractedText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length:N0}\n\n" +
            $"Extracted Text:\n" +
            $"{extractedText}";

            auditLogger.LogFileOperation("OCR_ExtractFromImage", fullPath, true);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to extract text from image: {ImagePath}", imagePath);
            auditLogger.LogFileOperation("OCR_ExtractFromImage", imagePath, false, ex.Message);
            return $"Failed to extract text from image: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Check if a PDF is likely scanned and needs OCR (requires Tesseract OCR to be installed)")]
    public string CheckPdfForScannedContent(
        [Description("Path to the PDF file - must be canonical")] string pdfPath,
        [Description("Optional password for encrypted PDFs")] string? password = null)
    {
        try
        {
            string fullPath = Path.GetFullPath(pdfPath);
            if (!securityManager.IsDirectoryAllowed(Path.GetDirectoryName(fullPath)!))
            {
                return $"Access denied to directory: {Path.GetDirectoryName(fullPath)}";
            }

            if (!File.Exists(fullPath))
            {
                return $"File not found: {fullPath}";
            }

            if (!Path.GetExtension(fullPath).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return $"File is not a PDF: {fullPath}";
            }

            if (!ocrService.IsAvailable)
            {
                return "OCR service is not available. Cannot analyze PDF for scanned content.";
            }

            bool isScanned = ocrService.IsPdfScanned(fullPath, password);
            var fileInfo = new FileInfo(fullPath);
            
            string result = $"PDF Scan Analysis Results\n" +
            $"File: {fileInfo.Name}\n" +
            $"Size: {fileInfo.Length:N0} bytes\n" +
            $"Is Likely Scanned: {(isScanned ? "Yes" : "No")}\n" +
            $"Recommendation: {(isScanned ? "Use OCR for text extraction" : "Standard text extraction should work")}\n";

            if (isScanned)
            {
                result += "\nNote: This PDF appears to contain scanned images. " +
            "Use extract_text_from_scanned_pdf for better text extraction.";
            }

            auditLogger.LogFileOperation("OCR_CheckPdfScanned", fullPath, true);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to analyze PDF: {PdfPath}", pdfPath);
            return $"Failed to analyze PDF: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Extract text from a scanned PDF using OCR (requires Tesseract OCR to be installed)")]
    public async Task<string> ExtractTextFromScannedPdf(
        [Description("Path to the PDF file - must be canonical")] string pdfPath,
        [Description("Optional password for encrypted PDFs")] string? password = null)
    {
        try
        {
            string fullPath = Path.GetFullPath(pdfPath);
            if (!securityManager.IsDirectoryAllowed(Path.GetDirectoryName(fullPath)!))
            {
                var error = $"Access denied to directory: {Path.GetDirectoryName(fullPath)}";
                auditLogger.LogFileOperation("OCR_ExtractFromPdf", fullPath, false, error);
                return error;
            }

            if (!File.Exists(fullPath))
            {
                var error = $"File not found: {fullPath}";
                auditLogger.LogFileOperation("OCR_ExtractFromPdf", fullPath, false, error);
                return error;
            }

            if (!Path.GetExtension(fullPath).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                var error = $"File is not a PDF: {fullPath}";
                auditLogger.LogFileOperation("OCR_ExtractFromPdf", fullPath, false, error);
                return error;
            }

            if (!ocrService.IsAvailable)
            {
                var warning = "OCR service is not available. Please ensure Tesseract is installed and tessdata is properly configured.";
                auditLogger.LogFileOperation("OCR_ExtractFromPdf", fullPath, false, warning);
                return warning;
            }

            string extractedText = await ocrService.ExtractTextFromScannedPdf(fullPath, password);
            var fileInfo = new FileInfo(fullPath);
            
            string result = $"OCR PDF Text Extraction Results\n" +
            $"PDF: {fileInfo.Name}\n" +
            $"File Size: {fileInfo.Length:N0} bytes\n" +
            $"Text Length: {extractedText.Length:N0} characters\n" +
            $"Word Count: {extractedText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length:N0}\n\n" +
            $"Extracted Text (first 2000 characters):\n" +
            $"{extractedText.Substring(0, Math.Min(2000, extractedText.Length))}";

            if (extractedText.Length > 2000)
            {
                result += $"\n\n... (truncated {extractedText.Length - 2000} more characters)";
            }

            auditLogger.LogFileOperation("OCR_ExtractFromPdf", fullPath, true);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to extract text from scanned PDF: {PdfPath}", pdfPath);
            auditLogger.LogFileOperation("OCR_ExtractFromPdf", pdfPath, false, ex.Message);
            return $"Failed to extract text from scanned PDF: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Check OCR service status and availability (use this to verify Tesseract OCR is installed and configured correctly)")]
    public Task<string> GetOcrServiceStatus()
    {
        try
        {
            var status = new StringBuilder();
            status.AppendLine("OCR Service Status Report");
            status.AppendLine("========================");
            status.AppendLine($"Service Available: {(ocrService.IsAvailable ? "Yes" : "No")}");
            
            if (!ocrService.IsAvailable)
            {
                status.AppendLine("\nOCR service is not available. This could be due to:");
                status.AppendLine("• Tesseract OCR not installed");
                status.AppendLine("• tessdata directory not found");
                status.AppendLine("• Initialization errors");
                status.AppendLine("\nTo enable OCR:");
                status.AppendLine("1. Install Tesseract OCR");
                status.AppendLine("2. Download tessdata files");
                status.AppendLine("3. Ensure tessdata is in a standard location or current directory");
            }
            else
            {
                status.AppendLine("✓ Tesseract OCR engine initialized successfully");
                status.AppendLine("✓ Ready to process images and scanned PDFs");
                
                status.AppendLine("\nSupported Operations:");
                status.AppendLine("• Extract text from images (JPG, PNG, BMP, TIFF, GIF)");
                status.AppendLine("• Analyze PDFs for scanned content");
                status.AppendLine("• Extract text from scanned PDFs");
                status.AppendLine("• Integration with document indexing");
            }

            return Task.FromResult(status.ToString());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get OCR service status");
            return Task.FromResult($"Failed to get OCR service status: {ex.Message}");
        }
    }

    private static bool IsImageFile(string filePath)
    {
        string extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or ".tiff" or ".tif";
    }

    #endregion

    #region Document Processing

    [McpServerTool]
    [Description("Extract content from a single document (supports password-protected files)")]
    public async Task<string> ExtractDocumentContent(
        [Description("Path to the document file - must be canonical")] string filePath,
        [Description("Optional password (will try auto-detected passwords first)")] string? password = null)
    {
        try
        {
            string fullPath = Path.GetFullPath(filePath);
            if (!securityManager.IsDirectoryAllowed(Path.GetDirectoryName(fullPath)!))
            {
                var error = $"Access denied to directory: {Path.GetDirectoryName(fullPath)}";
                auditLogger.LogFileOperation("ExtractContent", fullPath, false, error);
                return error;
            }

            if (!File.Exists(fullPath))
            {
                var error = $"File not found: {fullPath}";
                auditLogger.LogFileOperation("ExtractContent", fullPath, false, error);
                return error;
            }

            DocumentContent content = await documentProcessor.ExtractContent(fullPath, password);
            
            string result = $"Document: {content.FilePath}\n" +
                            $"Type: {content.DocumentType}\n" +
                            $"Title: {content.Title}\n" +
                            $"Size: {content.Metadata.FileSizeBytes:N0} bytes\n" +
                            $"Modified: {content.Metadata.ModifiedDate:yyyy-MM-dd HH:mm:ss}\n" +
                            $"Password Required: {content.RequiredPassword}\n" +
                            $"Extracted: {content.ExtractedAt:yyyy-MM-dd HH:mm:ss}\n\n" +
                            $"Content Preview (first 1000 chars):\n" +
                            $"{content.PlainText.Substring(0, Math.Min(1000, content.PlainText.Length))}";

            if (content.PlainText.Length > 1000)
            {
                result += $"\n\n... (truncated {content.PlainText.Length - 1000} more characters)";
            }

            auditLogger.LogFileOperation("ExtractContent", fullPath, true);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to extract content from: {FilePath}", filePath);
            auditLogger.LogFileOperation("ExtractContent", filePath, false, ex.Message);
            return $"Failed to extract document content: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Get detailed metadata from a document")]
    public async Task<string> GetDocumentMetadata(
        [Description("Path to the document file - must be canonical")] string filePath,
        [Description("Optional password")] string? password = null)
    {
        try
        {
            string fullPath = Path.GetFullPath(filePath);
            if (!securityManager.IsDirectoryAllowed(Path.GetDirectoryName(fullPath)!))
            {
                return $"Access denied to directory: {Path.GetDirectoryName(fullPath)}";
            }

            DocumentContent content = await documentProcessor.ExtractContent(fullPath, password);
            DocumentMetadata metadata = content.Metadata;

            string result = $"File: {metadata.FileName}\n" +
                            $"Path: {content.FilePath}\n" +
                            $"Document Type: {content.DocumentType}\n" +
                            $"Title: {content.Title}\n" +
                            $"Size: {metadata.FileSizeBytes:N0} bytes\n" +
                            $"Created: {metadata.CreatedDate:yyyy-MM-dd HH:mm:ss}\n" +
                            $"Modified: {metadata.ModifiedDate:yyyy-MM-dd HH:mm:ss}\n" +
                            $"Accessed: {metadata.AccessedDate:yyyy-MM-dd HH:mm:ss}\n";

            if (!string.IsNullOrWhiteSpace(metadata.Author))
                result += $"Author: {metadata.Author}\n";
            
            if (!string.IsNullOrWhiteSpace(metadata.Subject))
                result += $"Subject: {metadata.Subject}\n";
            
            if (!string.IsNullOrWhiteSpace(metadata.Keywords))
                result += $"Keywords: {metadata.Keywords}\n";
            
            if (metadata.PageCount > 0)
                result += $"Pages: {metadata.PageCount}\n";
            
            if (metadata.WordCount > 0)
                result += $"Words: {metadata.WordCount:N0}\n";
            
            if (metadata.CharacterCount > 0)
                result += $"Characters: {metadata.CharacterCount:N0}\n";

            if (content.Sections.Count != 0)
                result += $"Sections: {content.Sections.Count}\n";
            
            if (content.StructuredData.Count != 0)
                result += $"Structured Data: {string.Join(", ", content.StructuredData.Keys)}\n";

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get metadata from: {FilePath}", filePath);
            return $"Failed to get document metadata: {ex.Message}";
        }
    }

    #endregion

    #region Document Indexing

    [McpServerTool]
    [Description("Create a searchable index from a directory of documents")]
    public async Task<string> CreateDocumentIndex(
        [Description("Name for the search index")] string indexName,
        [Description("Root directory containing documents to index - must be canonical")] string rootPath,
        [Description("JSON options for indexing (optional)")] string? optionsJson = null)
    {
        try
        {
            string fullPath = Path.GetFullPath(rootPath);
            if (!securityManager.IsDirectoryAllowed(fullPath))
            {
                var error = $"Access denied to directory: {fullPath}";
                auditLogger.LogIndexOperation("CreateIndex", indexName, false, error);
                return error;
            }

            IndexingOptions? options = null;
            if (!string.IsNullOrWhiteSpace(optionsJson))
            {
                try
                {
                    options = JsonSerializer.Deserialize<IndexingOptions>(optionsJson);
                }
                catch (Exception ex)
                {
                    return $"Invalid options JSON: {ex.Message}";
                }
            }

            IndexingResult result = await documentIndexer.BuildIndex(indexName, fullPath, options);
            
            string response = $"Index '{indexName}' created successfully!\n" +
                              $"Root Path: {result.RootPath}\n" +
                              $"Duration: {result.Duration.TotalMinutes:F1} minutes\n" +
                              $"Successful: {result.Successful.Count:N0} documents\n" +
                              $"Failed: {result.Failed.Count:N0} documents\n" +
                              $"Password Protected: {result.PasswordProtected.Count:N0} documents\n" +
                              $"Total Size: {result.TotalSizeBytes / (1024 * 1024):N1} MB\n\n";

            if (result.FileTypeStats.Count != 0)
            {
                response += "File Types:\n";
                foreach ((string type, int count) in result.FileTypeStats.OrderByDescending(x => x.Value))
                {
                    response += $"  {type}: {count:N0}\n";
                }
                response += "\n";
            }

            if (result.Failed.Count != 0)
            {
                response += "Failed Documents (first 5):\n";
                response = result.Failed.Take(5).Aggregate(response, (current, failed) => current + $"  {Path.GetFileName(failed.FilePath)}: {failed.ErrorMessage}\n");
                if (result.Failed.Count > 5)
                {
                    response += $"  ... and {result.Failed.Count - 5} more\n";
                }
            }

            auditLogger.LogIndexOperation("CreateIndex", indexName, true);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create index: {IndexName}", indexName);
            auditLogger.LogIndexOperation("CreateIndex", indexName, false, ex.Message);
            return $"Failed to create index: {ex.Message}";
        }
    }
    
    [McpServerTool]
    [Description("Check for existing indexes and suggest which one to use for a directory")]
    public async Task<string> FindIndexForDirectory(
        [Description("Directory path to check for existing indexes - must be canonical")] string directoryPath)
    {
        try
        {
            string fullPath = Path.GetFullPath(directoryPath);
            if (!Directory.Exists(fullPath))
            {
                return $"Directory not found: {fullPath}";
            }

            // Get all available indexes
            List<string> indexes = await documentIndexer.GetIndexNames();
            
            if (indexes.Count == 0)
            {
                return $"No existing indexes found. You can create a new index for directory: {directoryPath}";
            }

            var response = new StringBuilder();
            response.AppendLine($"Checking for existing indexes that might cover: {directoryPath}\n");
            response.AppendLine($"Available indexes ({indexes.Count}):");
            
            var potentialMatches = new List<string>();
            
            foreach (string indexName in indexes.OrderBy(x => x))
            {
                // Simple heuristic: check if index name suggests it covers this directory
                string lowerIndexName = indexName.ToLowerInvariant();
                string directoryName = Path.GetFileName(directoryPath).ToLowerInvariant();
                
                bool isPotentialMatch = lowerIndexName.Contains(directoryName) || 
                                      lowerIndexName.Contains("automotive") ||
                                      lowerIndexName.Contains("manual") ||
                                      lowerIndexName.Contains("document");
                
                if (isPotentialMatch)
                {
                    potentialMatches.Add(indexName);
                    response.AppendLine($"  ✅ {indexName} (potential match)");
                }
                else
                {
                    response.AppendLine($"  • {indexName}");
                }
            }
            
            response.AppendLine();
            
            if (potentialMatches.Count > 0)
            {
                response.AppendLine("🎯 Recommended: Use one of the potential matches above before creating a new index.");
                response.AppendLine("   You can test with: AnswerQuestionFromDocuments using the existing index name.");
                response.AppendLine();
                response.AppendLine("💡 To check what's in an index, try searching for a broad term first.");
            }
            else
            {
                response.AppendLine("No obvious matches found. You may need to:");
                response.AppendLine("  1. Create a new index for this directory, OR");
                response.AppendLine("  2. Check if one of the existing indexes actually covers this content");
            }
            
            return response.ToString();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to find index for directory: {DirectoryPath}", directoryPath);
            return $"Failed to find index for directory: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Test search in an existing index to see what content it contains")]
    public string TestIndexContent(
        [Description("Name of the index to test")] string indexName,
        [Description("Broad search term to test (e.g., 'ford', 'automotive', 'manual')")] string testQuery)
    {
        try
        {
            // Perform a broad search to see what content is in the index
            SearchResults results = documentIndexer.Search(testQuery, indexName);
            
            var response = new StringBuilder();
            response.AppendLine($"Testing index '{indexName}' with query: '{testQuery}'");
            response.AppendLine($"Found {results.TotalHits} total results\n");
            
            if (results.Results.Count == 0)
            {
                response.AppendLine("❌ No results found. This index may not contain relevant content for your query.");
                response.AppendLine("   Try a different search term or create a new index.");
                return response.ToString();
            }
            
            response.AppendLine("📁 Sample content in this index:");
            
            // Show first few results to give user an idea of what's in the index
            for (int i = 0; i < Math.Min(5, results.Results.Count); i++)
            {
                var result = results.Results[i];
                response.AppendLine($"  {i + 1}. {result.Title}");
                response.AppendLine($"     Path: {Path.GetDirectoryName(result.FilePath)}");
                response.AppendLine($"     File: {Path.GetFileName(result.FilePath)}");
                response.AppendLine($"     Type: {result.DocumentType}");
                if (result.Snippets.Any())
                {
                    response.AppendLine($"     Preview: \"{result.Snippets.First()}\"");
                }
                response.AppendLine();
            }
            
            if (results.Results.Count > 5)
            {
                response.AppendLine($"... and {results.Results.Count - 5} more results");
            }
            
            response.AppendLine("✅ This index appears to contain relevant content!");
            response.AppendLine("   You can use this index for AnswerQuestionFromDocuments.");
            
            auditLogger.LogSearchOperation("TestIndex", indexName, testQuery, true, results.TotalHits);
            return response.ToString();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to test index content: {IndexName} with query: {TestQuery}", indexName, testQuery);
            auditLogger.LogSearchOperation("TestIndex", indexName, testQuery, false);
            return $"Failed to test index: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Search documents in an index using natural language queries")]
    public string SearchDocuments(
        [Description("Search query (natural language or keywords)")] string query,
        [Description("Name of the index to search")] string indexName,
        [Description("JSON search options (optional)")] string? searchOptionsJson = null)
    {
        try
        {
            SearchQuery? searchQuery = null;
            if (!string.IsNullOrWhiteSpace(searchOptionsJson))
            {
                try
                {
                    searchQuery = JsonSerializer.Deserialize<SearchQuery>(searchOptionsJson);
                    if (searchQuery != null)
                    {
                        searchQuery.Query = query; // Ensure query is set
                    }
                }
                catch (Exception ex)
                {
                    return $"Invalid search options JSON: {ex.Message}";
                }
            }

            SearchResults results = documentIndexer.Search(query, indexName, searchQuery);
            
            string response = $"Search Results for: \"{query}\"\n" +
                              $"Index: {indexName}\n" +
                              $"Total Hits: {results.TotalHits:N0}\n" +
                              $"Search Time: {results.SearchTimeMs:F0}ms\n" +
                              $"Showing: {results.Results.Count} results\n\n";

            for (var i = 0; i < results.Results.Count; i++)
            {
                SearchResult result = results.Results[i];
                response += $"{i + 1}. {result.Title}\n" +
                           $"   File: {Path.GetFileName(result.FilePath)}\n" +
                           $"   Path: {Path.GetDirectoryName(result.FilePath)}\n" +
                           $"   Type: {result.DocumentType}\n" +
                           $"   Score: {result.RelevanceScore:F2}\n" +
                           $"   Modified: {result.ModifiedDate:yyyy-MM-dd}\n" +
                           $"   Size: {result.FileSizeBytes:N0} bytes\n";

                if (result.Snippets.Count != 0)
                {
                    response += "   Snippets:\n";
                    response = result.Snippets.Take(2).Aggregate(response, (current, snippet) => current + $"     \"{snippet}\"\n");
                }

                response += "\n";
            }

            if (results.FileTypeCounts.Count != 0)
            {
                response += "File Types in Results:\n";
                foreach ((string type, int count) in results.FileTypeCounts)
                {
                    response += $"  {type}: {count}\n";
                }
            }

            auditLogger.LogSearchOperation("Search", indexName, query, true, results.TotalHits);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Search failed: {Query} in {IndexName}", query, indexName);
            auditLogger.LogSearchOperation("Search", indexName, query, false);
            return $"Search failed: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("List all available document indexes")]
    public async Task<string> ListDocumentIndexes()
    {
        try
        {
            List<string> indexes = await documentIndexer.GetIndexNames();
            
            if (indexes.Count == 0)
            {
                return "No document indexes found. Use create_document_index to create one.";
            }

            var response = $"Available Document Indexes ({indexes.Count}):\n\n";

            return indexes.OrderBy(x => x).Aggregate(response, (current, indexName) => current + $"• {indexName}\n");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list indexes");
            return $"Failed to list indexes: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Get memory status of all document indexes showing which are loaded vs discoverable")]
    public async Task<string> GetIndexMemoryStatus()
    {
        try
        {
            Dictionary<string, IndexMemoryStatus> status = await documentIndexer.GetIndexMemoryStatus();
            
            if (status.Count == 0)
            {
                return "No document indexes found.";
            }

            var response = $"Index Memory Status ({status.Count} indexes):\n\n";
            
            var totalMemoryMb = 0.0;
            var loadedCount = 0;
            var discoverableCount = 0;
            
            foreach ((string indexName, IndexMemoryStatus indexStatus) in status.OrderBy(x => x.Key))
            {
                string memoryInfo = indexStatus.EstimatedMemoryUsageMb > 0 
                    ? $" ({indexStatus.EstimatedMemoryUsageMb:F1} MB)" 
                    : "";
                
                response += $"• {indexName}: {indexStatus.Status}{memoryInfo}\n";
                
                if (indexStatus.IsLoadedInMemory)
                {
                    loadedCount++;
                    totalMemoryMb += indexStatus.EstimatedMemoryUsageMb;
                }
                else if (indexStatus.IsDiscovered)
                {
                    discoverableCount++;
                }
            }
            
            response += $"\nSummary:\n";
            response += $"  Loaded in Memory: {loadedCount} (~{totalMemoryMb:F1} MB)\n";
            response += $"  Discoverable: {discoverableCount}\n";
            response += $"  Total Memory Usage: {totalMemoryMb:F1} MB\n";
            
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get index memory status");
            return $"Failed to get index memory status: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Unload a specific index from memory while keeping it discoverable for future use")]
    public string UnloadIndexFromMemory(
        [Description("Name of the index to unload from memory")] string indexName)
    {
        try
        {
            bool wasUnloaded = documentIndexer.UnloadIndex(indexName);
            
            if (wasUnloaded)
            {
                auditLogger.LogIndexOperation("UnloadIndex", indexName, true);
                return $"Index '{indexName}' unloaded from memory successfully. It remains discoverable and will be lazy-loaded when next accessed.";
            }
            else
            {
                return $"Index '{indexName}' was not loaded in memory (no action needed).";
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to unload index: {IndexName}", indexName);
            auditLogger.LogIndexOperation("UnloadIndex", indexName, false, ex.Message);
            return $"Failed to unload index: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Unload all indexes from memory to free resources while keeping them discoverable")]
    public string UnloadAllIndexesFromMemory()
    {
        try
        {
            int unloadedCount = documentIndexer.UnloadAllIndexes();
            
            if (unloadedCount > 0)
            {
                auditLogger.LogIndexOperation("UnloadAllIndexes", "ALL", true, $"Unloaded {unloadedCount} indexes");
                return $"Successfully unloaded {unloadedCount} indexes from memory. All indexes remain discoverable and will be lazy-loaded when accessed.";
            }
            return "No indexes were loaded in memory (no action needed).";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to unload all indexes");
            auditLogger.LogIndexOperation("UnloadAllIndexes", "ALL", false, ex.Message);
            return $"Failed to unload all indexes: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Permanently remove a document index from the system (both memory and discovery). Use unload_index_from_memory if you only want to free memory.")]
    public string RemoveDocumentIndex(
        [Description("Name of the index to permanently remove")] string indexName)
    {
        try
        {
            bool removed = documentIndexer.RemoveIndex(indexName);
            
            if (removed)
            {
                auditLogger.LogIndexOperation("RemoveIndex", indexName, true);
                return $"Index '{indexName}' permanently removed from system.";
            }

            return $"Index '{indexName}' not found.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to remove index: {IndexName}", indexName);
            auditLogger.LogIndexOperation("RemoveIndex", indexName, false, ex.Message);
            return $"Failed to remove index: {ex.Message}";
        }
    }

    #endregion

    #region Utility Functions

    [McpServerTool]
    [Description("Discover documents in a directory (without indexing)")]
    public Task<string> DiscoverDocuments(
        [Description("Root directory to search - must be canonical")] string rootPath,
        [Description("File patterns to include (comma-separated, e.g., '*.pdf,*.docx')")] string includePatterns = "*",
        [Description("Whether to search subdirectories")] bool recursive = true)
    {
        try
        {
            string fullPath = Path.GetFullPath(rootPath);
            if (!securityManager.IsDirectoryAllowed(fullPath))
            {
                return Task.FromResult($"Access denied to directory: {fullPath}");
            }

            if (!Directory.Exists(fullPath))
            {
                return Task.FromResult($"Directory not found: {fullPath}");
            }

            string[] patterns = includePatterns.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim()).ToArray();
            
            var documents = new List<FileInfo>();
            SearchOption searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            
            foreach (string pattern in patterns)
            {
                string[] files = Directory.GetFiles(fullPath, pattern, searchOption);
                documents.AddRange(files.Select(f => new FileInfo(f)));
            }

            documents = documents.DistinctBy(d => d.FullName).OrderBy(d => d.FullName).ToList();
            
            string response = $"Document Discovery Results\n" +
                              $"Directory: {fullPath}\n" +
                              $"Patterns: {string.Join(", ", patterns)}\n" +
                              $"Recursive: {recursive}\n" +
                              $"Found: {documents.Count:N0} documents\n\n";

            IOrderedEnumerable<IGrouping<string, FileInfo>> typeGroups = documents.GroupBy(d => d.Extension.ToLowerInvariant())
                .OrderByDescending(g => g.Count());
            
            response += "File Types:\n";
            foreach (IGrouping<string, FileInfo> group in typeGroups)
            {
                string extension = string.IsNullOrEmpty(group.Key) ? "(no extension)" : group.Key;
                response += $"  {extension}: {group.Count():N0} files\n";
            }

            long totalSize = documents.Sum(d => d.Length);
            response += $"\nTotal Size: {totalSize / (1024.0 * 1024):F1} MB\n";

            if (documents.Count <= 20)
            {
                response += "\nFiles:\n";
                foreach (FileInfo doc in documents)
                {
                    response += $"  {Path.GetRelativePath(fullPath, doc.FullName)} " +
                               $"({doc.Length:N0} bytes, {doc.LastWriteTime:yyyy-MM-dd})\n";
                }
            }
            else
            {
                response += $"\n(Showing file types only - {documents.Count} total files)";
            }

            return Task.FromResult(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to discover documents: {RootPath}", rootPath);
            return Task.FromResult($"Failed to discover documents: {ex.Message}");
        }
    }

    [McpServerTool]
    [Description("Get registered password patterns (passwords are masked)")]
    public Task<string> GetRegisteredPasswords()
    {
        try
        {
            Dictionary<string, string> patterns = passwordManager.GetRegisteredPatterns();
            
            if (patterns.Count == 0)
            {
                return Task.FromResult("No password patterns registered. Use register_password_pattern to add some.");
            }

            var response = $"Registered Password Patterns ({patterns.Count}):\n\n";
            foreach ((string pattern, string maskedPassword) in patterns.OrderBy(x => x.Key))
            {
                response += $"• {pattern} → {maskedPassword}\n";
            }

            return Task.FromResult(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get registered passwords");
            return Task.FromResult($"Failed to get registered passwords: {ex.Message}");
        }
    }


    #region Hybrid Question Answering

    [McpServerTool]
    [Description("Answer questions using document content - supports both AI summary and document viewing")]
    public async Task<string> AnswerQuestionFromDocuments(
        [Description("User question (e.g., 'How do I...' or 'Show me how to...')")] string question,
        [Description("Name of the index to search")] string indexName,
        [Description("Maximum number of documents to process")] int maxDocuments = 3)
    {
        try
        {
            // Detect user intent
            bool showDocuments = IsShowIntent(question);
            
            if (showDocuments)
            {
                return await ShowRelevantDocuments(question, indexName, maxDocuments);
            }
            else
            {
                return await ProvideAISummary(question, indexName, maxDocuments);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AnswerQuestionFromDocuments failed: {Question}", question);
            auditLogger.LogSearchOperation("QuestionAnswer", indexName, question, false);
            return $"Failed to answer question: {ex.Message}";
        }
    }

    private static bool IsShowIntent(string question)
    {
        string lowerQuestion = question.ToLowerInvariant();
        return lowerQuestion.StartsWith("show me") ||
          lowerQuestion.Contains("show me how") ||
          lowerQuestion.StartsWith("display") ||
          lowerQuestion.Contains("open the") ||
          lowerQuestion.Contains("let me see") ||
          lowerQuestion.Contains("show me the");
    }

    private async Task<string> ProvideAISummary(string question, string indexName, int maxDocuments)
    {
        try
        {
            // Search for relevant documents
            SearchResults searchResults = documentIndexer.Search(question, indexName);
            
            if (searchResults.Results.Count == 0)
            {
                auditLogger.LogSearchOperation("AISummary", indexName, question, true, 0);
                return $"No relevant documents found for: \"{question}\"";
            }

            // Extract content for AI consumption
            var contentForAI = new StringBuilder();
            contentForAI.AppendLine($"=== CONTENT FOR AI ANALYSIS ===");
            contentForAI.AppendLine($"User Question: {question}");
            contentForAI.AppendLine("=== RELEVANT DOCUMENTS ===\n");
            
            var processedCount = 0;
            foreach (SearchResult result in searchResults.Results.Take(maxDocuments))
            {
                try
                {
                    // Security check
                    if (!securityManager.IsDirectoryAllowed(Path.GetDirectoryName(result.FilePath)!))
                    {
                        contentForAI.AppendLine($"❌ Access denied: {result.Title}\n");
                        continue;
                    }

                    DocumentContent content = await documentProcessor.ExtractContent(result.FilePath);
                    
                    contentForAI.AppendLine($"📄 DOCUMENT {processedCount + 1}: {result.Title}");
                    contentForAI.AppendLine($"Source: {Path.GetFileName(result.FilePath)}");
                    contentForAI.AppendLine($"Path: {result.FilePath}");
                    contentForAI.AppendLine($"Type: {result.DocumentType}");
                    contentForAI.AppendLine($"Relevance Score: {result.RelevanceScore:F2}");
                    contentForAI.AppendLine($"Modified: {result.ModifiedDate:yyyy-MM-dd}");
                    contentForAI.AppendLine("--- FULL CONTENT ---");
                    contentForAI.AppendLine(content.PlainText);
                    contentForAI.AppendLine("\n" + new string('=', 80) + "\n");
                    
                    processedCount++;
                }
                catch (Exception ex)
                {
                    contentForAI.AppendLine($"❌ Failed to extract content from {result.Title}: {ex.Message}\n");
                }
            }
            
            contentForAI.AppendLine($"=== END OF RETRIEVED CONTENT ({processedCount} documents) ===");
            contentForAI.AppendLine("\nNOTE: Please analyze the above content and provide a comprehensive answer to the user's question.");
            
            auditLogger.LogSearchOperation("AISummary", indexName, question, true, processedCount);
            return contentForAI.ToString();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ProvideAISummary failed: {Question}", question);
            auditLogger.LogSearchOperation("AISummary", indexName, question, false);
            return $"Failed to provide AI summary: {ex.Message}";
        }
    }

    private async Task<string> ShowRelevantDocuments(string question, string indexName, int maxDocuments)
    {
        try
        {
            // Search for relevant documents
            SearchResults searchResults = documentIndexer.Search(question, indexName);
            
            if (searchResults.Results.Count == 0)
            {
                auditLogger.LogSearchOperation("ShowDocuments", indexName, question, true, 0);
                return $"No relevant documents found for: \"{question}\"";
            }

            var response = new StringBuilder();
            response.AppendLine($"Opening relevant documents for: \"{question}\"\n");
            
            var openedCount = 0;
            foreach (SearchResult result in searchResults.Results.Take(maxDocuments))
            {
                try
                {
                    // Security check
                    if (!securityManager.IsDirectoryAllowed(Path.GetDirectoryName(result.FilePath)!))
                    {
                        response.AppendLine($"❌ Access denied: {result.Title}");
                        continue;
                    }

                    if (!File.Exists(result.FilePath))
                    {
                        response.AppendLine($"❌ File not found: {result.Title}");
                        continue;
                    }

                    // Try to open to relevant section
                    bool opened = await TryOpenDocumentToRelevantSection(result, question);
                    
                    if (opened)
                    {
                        response.AppendLine($"✅ Opened: {result.Title}");
                        response.AppendLine($"   File: {Path.GetFileName(result.FilePath)}");
                        response.AppendLine($"   Path: {Path.GetDirectoryName(result.FilePath)}");
                        response.AppendLine($"   Type: {result.DocumentType}");
                        response.AppendLine($"   Relevance: {result.RelevanceScore:F2}");
                        if (result.Snippets.Count != 0)
                        {
                            response.AppendLine($"   Preview: \"{result.Snippets.First()}\"");
                        }
                        response.AppendLine();
                        openedCount++;
                        
                        auditLogger.LogFileOperation("OpenDocument", result.FilePath, true);
                    }
                    else
                    {
                        response.AppendLine($"❌ Failed to open: {result.Title}");
                        auditLogger.LogFileOperation("OpenDocument", result.FilePath, false, "Process.Start failed");
                    }
                }
                catch (Exception ex)
                {
                    response.AppendLine($"❌ Error opening {result.Title}: {ex.Message}");
                    auditLogger.LogFileOperation("OpenDocument", result.FilePath, false, ex.Message);
                }
            }
            
            response.AppendLine($"Summary: Successfully opened {openedCount} of {Math.Min(maxDocuments, searchResults.Results.Count)} documents.");
            
            auditLogger.LogSearchOperation("ShowDocuments", indexName, question, true, openedCount);
            return response.ToString();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ShowRelevantDocuments failed: {Question}", question);
            auditLogger.LogSearchOperation("ShowDocuments", indexName, question, false);
            return $"Failed to show documents: {ex.Message}";
        }
    }

    private async Task<bool> TryOpenDocumentToRelevantSection(SearchResult result, string question)
    {
        try
        {
            string extension = Path.GetExtension(result.FilePath).ToLowerInvariant();

            switch (extension)
            {
                // For PDFs, try to open to a specific page if we can determine it
                case ".pdf":
                    return await TryOpenPdfToRelevantPage(result, question);
                // For Excel files, try to open to relevant worksheet
                case ".xlsx" or ".xls":
                    return await TryOpenExcelToRelevantSheet(result, question);
                // For Word files, try to open with search or navigation
                case ".docx" or ".doc":
                    return await TryOpenWordWithNavigation(result, question);
                // For PowerPoint files, try to open to relevant slide
                case ".pptx" or ".ppt":
                    return await TryOpenPowerPointToRelevantSlide(result, question);
            }

            // For other documents, just open normally
            var startInfo = new ProcessStartInfo
            {
                FileName = result.FilePath,
                UseShellExecute = true,
                Verb = "open"
            };

            Process? process = Process.Start(startInfo);
            return process != null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to open document: {FilePath}", result.FilePath);
            return false;
        }
    }

    private async Task<bool> TryOpenPdfToRelevantPage(SearchResult result, string question)
    {
        try
        {
            // Try to determine the relevant page from snippets or search within the document
            int pageNumber = await EstimateRelevantPage(result.FilePath, question);
            
            if (pageNumber > 0)
            {
                // Try different PDF viewers with page parameters
                var pdfViewerCommands = new[]
                {
                    // Adobe Acrobat/Reader
                    $"\"C:\\Program Files\\Adobe\\Acrobat DC\\Acrobat\\Acrobat.exe\" /A \"page={pageNumber}\" \"{result.FilePath}\"",
                    $"\"C:\\Program Files (x86)\\Adobe\\Acrobat Reader DC\\Reader\\AcroRd32.exe\" /A \"page={pageNumber}\" \"{result.FilePath}\"",
                    // Foxit Reader
                    $"\"C:\\Program Files\\Foxit Software\\Foxit Reader\\FoxitReader.exe\" /A page={pageNumber} \"{result.FilePath}\"",
                    $"\"C:\\Program Files (x86)\\Foxit Software\\Foxit Reader\\FoxitReader.exe\" /A page={pageNumber} \"{result.FilePath}\"",
                    // SumatraPDF
                    $"SumatraPDF.exe -page {pageNumber} \"{result.FilePath}\"",
                    // Microsoft Edge (if available)
                    $"msedge.exe --app=\"file:///{result.FilePath.Replace('\\', '/')}#page={pageNumber}\""
                };
                
                foreach (string viewerCommand in pdfViewerCommands)
                {
                    try
                    {
                        Process? process = Process.Start(new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = $"/c {viewerCommand}",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            WindowStyle = ProcessWindowStyle.Hidden
                        });
                        
                        if (process is not null)
                        {
                            // Give it a moment to start
                            await Task.Delay(1000);
                            return true;
                        }
                    }
                    catch
                    {
                        // Try the next viewer
                        continue;
                    }
                }
            }
            
            // Fall back to default opening
            Process? fallbackProcess = Process.Start(new ProcessStartInfo
            {
                FileName = result.FilePath,
                UseShellExecute = true
            });
            
            return fallbackProcess != null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to open PDF with page navigation: {FilePath}", result.FilePath);
            
            // Last resort - try basic opening
            try
            {
                Process? basicProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = result.FilePath,
                    UseShellExecute = true
                });
                return basicProcess != null;
            }
            catch
            {
                return false;
            }
        }
    }

    private async Task<int> EstimateRelevantPage(string filePath, string question)
    {
        try
        {
            await Task.Delay(1); // Make it properly async
            
            // Extract keywords from the question for searching
            List<string> keywords = ExtractSearchKeywords(question);
            if (keywords.Count == 0)
            {
                return 1; // Default to the first page if no keywords
            }

            // Try to find the password for this PDF if needed
            string? password = passwordManager.GetPasswordForFile(filePath);
            
            // Open PDF and analyze pages for keyword relevance
            var readerProperties = new ReaderProperties();
            if (!string.IsNullOrEmpty(password))
            {
                readerProperties.SetPassword(Encoding.UTF8.GetBytes(password));
            }

            using var pdfReader = new PdfReader(filePath, readerProperties);
            using var pdfDocument = new PdfDocument(pdfReader);

            int pageCount = pdfDocument.GetNumberOfPages();
            var pageScores = new Dictionary<int, double>();
            
            // Analyze each page for keyword relevance
            for (var pageNum = 1; pageNum <= pageCount; pageNum++)
            {
                try
                {
                    string pageText = PdfTextExtractor.GetTextFromPage(pdfDocument.GetPage(pageNum));
                    double score = CalculateRelevanceScore(pageText, keywords);
                    pageScores[pageNum] = score;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to extract text from page {PageNum} of {FilePath}", pageNum, filePath);
                    pageScores[pageNum] = 0; // Score of 0 for failed pages
                }
            }
            
            // Return the page with the highest relevance score, or page 1 if all scores are 0
            KeyValuePair<int, double> bestPage = pageScores.OrderByDescending(p => p.Value).FirstOrDefault();
            int resultPage = bestPage.Value > 0 ? bestPage.Key : 1;
            
            logger.LogDebug("Estimated relevant page {Page} for question '{Question}' in {FilePath} (score: {Score})", 
                resultPage, question, Path.GetFileName(filePath), bestPage.Value);
                
            return resultPage;
        }
        catch (Exception ex) when (ex.Message.Contains("password") || ex.Message.Contains("encrypted"))
        {
            logger.LogWarning("Password required for PDF page analysis: {FilePath}", filePath);
            return 1; // Default to the first page for password-protected files
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to estimate relevant page for: {FilePath}", filePath);
            return 1; // Default to first page on error
        }
    }

    private static List<string> ExtractSearchKeywords(string question)
    {
        // Remove common stop words and extract meaningful keywords
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "how", "do", "i", "to", "the", "a", "an", "and", "or", "but", "in", "on", "at", "is", "are", "was", "were",
            "show", "me", "what", "where", "when", "why", "can", "could", "should", "would", "will", "my", "your", "his", "her"
        };

        List<string> keywords = question
            .Split([' ', ',', '.', '?', '!', ';', ':', '-', '_'], StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word.Length > 2 && !stopWords.Contains(word))
            .Select(word => word.ToLowerInvariant())
            .Distinct()
            .ToList();

        return keywords;
    }

    private static double CalculateRelevanceScore(string pageText, List<string> keywords)
    {
        if (string.IsNullOrWhiteSpace(pageText) || keywords.Count == 0)
        {
            return 0;
        }

        string lowerPageText = pageText.ToLowerInvariant();
        double score = 0;

        foreach (string keyword in keywords)
        {
            // Count occurrences of each keyword
            int occurrences = CountOccurrences(lowerPageText, keyword);
            if (occurrences > 0)
            {
                // Base score + bonus for multiple occurrences + bonus for longer keywords
                double keywordScore = 1.0 + (occurrences - 1) * 0.5 + (keyword.Length - 3) * 0.1;
                score += keywordScore;

                // Bonus if keyword appears in first 200 characters (likely header/title area)
                if (lowerPageText.Length > 200 && lowerPageText.Substring(0, 200).Contains(keyword))
                {
                    score += 2.0;
                }
            }
        }

        // Normalize the score by page length to favor pages with higher keyword density
        double normalizedScore = score / Math.Max(1, pageText.Length / 1000.0);
        return normalizedScore;
    }

    private async Task<bool> TryOpenExcelToRelevantSheet(SearchResult result, string question)
    {
        try
        {
            // Extract content to analyze worksheet relevance
            DocumentContent content = await documentProcessor.ExtractContent(result.FilePath);
            
            if (content.StructuredData is not null && content.StructuredData.Count > 0)
            {
                List<string> keywords = ExtractSearchKeywords(question);
                string? bestSheet = FindMostRelevantSheet(content.StructuredData, keywords);
                
                if (!string.IsNullOrEmpty(bestSheet))
                {
                    // Try different Excel viewers with worksheet navigation
                    var excelCommands = new[]
                    {
                        // Microsoft Excel
                        $"excel.exe \"{result.FilePath}\" /e \"{bestSheet}\"",
                        $"\"C:\\Program Files\\Microsoft Office\\root\\Office16\\EXCEL.EXE\" \"{result.FilePath}\" /e \"{bestSheet}\"",
                        $"\"C:\\Program Files (x86)\\Microsoft Office\\root\\Office16\\EXCEL.EXE\" \"{result.FilePath}\" /e \"{bestSheet}\"",
                        // Office 2019/2021 paths
                        $"\"C:\\Program Files\\Microsoft Office\\Office16\\EXCEL.EXE\" \"{result.FilePath}\" /e \"{bestSheet}\"",
                        $"\"C:\\Program Files (x86)\\Microsoft Office\\Office16\\EXCEL.EXE\" \"{result.FilePath}\" /e \"{bestSheet}\"",
                    };
                    
                    foreach (string command in excelCommands)
                    {
                        try
                        {
                            Process? process = Process.Start(new ProcessStartInfo
                            {
                                FileName = "cmd.exe",
                                Arguments = $"/c {command}",
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                WindowStyle = ProcessWindowStyle.Hidden
                            });
                            
                            if (process != null)
                            {
                                await Task.Delay(1000);
                                logger.LogDebug("Opened Excel file to sheet '{Sheet}': {FilePath}", bestSheet, result.FilePath);
                                return true;
                            }
                        }
                        catch
                        {
                            // Try the next command
                            continue;
                        }
                    }
                }
            }
            
            // Fall back to normal opening
            Process? fallbackProcess = Process.Start(new ProcessStartInfo
            {
                FileName = result.FilePath,
                UseShellExecute = true
            });
            
            return fallbackProcess != null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to open Excel file with sheet navigation: {FilePath}", result.FilePath);
            
            // Last resort - try basic opening
            try
            {
                Process? basicProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = result.FilePath,
                    UseShellExecute = true
                });
                return basicProcess != null;
            }
            catch
            {
                return false;
            }
        }
    }

    private async Task<bool> TryOpenWordWithNavigation(SearchResult result, string question)
    {
        try
        {
            List<string> keywords = ExtractSearchKeywords(question);
            if (keywords.Count > 0)
            {
                // Try Word with search parameter - this will open and immediately search for the first keyword
                string searchTerm = keywords.First();
                
                var wordCommands = new[]
                {
                    // Microsoft Word with search
                    $"winword.exe \"{result.FilePath}\" /f \"{searchTerm}\"",
                    $"\"C:\\Program Files\\Microsoft Office\\root\\Office16\\WINWORD.EXE\" \"{result.FilePath}\" /f \"{searchTerm}\"",
                    $"\"C:\\Program Files (x86)\\Microsoft Office\\root\\Office16\\WINWORD.EXE\" \"{result.FilePath}\" /f \"{searchTerm}\"",
                    // Office 2019/2021 paths
                    $"\"C:\\Program Files\\Microsoft Office\\Office16\\WINWORD.EXE\" \"{result.FilePath}\" /f \"{searchTerm}\"",
                    $"\"C:\\Program Files (x86)\\Microsoft Office\\Office16\\WINWORD.EXE\" \"{result.FilePath}\" /f \"{searchTerm}\"",
                };
                
                foreach (string command in wordCommands)
                {
                    try
                    {
                        Process? process = Process.Start(new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = $"/c {command}",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            WindowStyle = ProcessWindowStyle.Hidden
                        });
                        
                        if (process != null)
                        {
                            await Task.Delay(1000);
                            logger.LogDebug("Opened Word file with search for '{SearchTerm}': {FilePath}", searchTerm, result.FilePath);
                            return true;
                        }
                    }
                    catch
                    {
                        // Try the next command
                        continue;
                    }
                }
            }
            
            // Fall back to normal opening
            Process? fallbackProcess = Process.Start(new ProcessStartInfo
            {
                FileName = result.FilePath,
                UseShellExecute = true
            });
            
            return fallbackProcess != null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to open Word file with navigation: {FilePath}", result.FilePath);
            
            // Last resort - try basic opening
            try
            {
                Process? basicProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = result.FilePath,
                    UseShellExecute = true
                });
                return basicProcess != null;
            }
            catch
            {
                return false;
            }
        }
    }

    private async Task<bool> TryOpenPowerPointToRelevantSlide(SearchResult result, string question)
    {
        try
        {
            // For PowerPoint, we'll try to open with search functionality
            List<string> keywords = ExtractSearchKeywords(question);
            if (keywords.Count > 0)
            {
                string searchTerm = keywords.First();
                
                var powerpointCommands = new[]
                {
                    // Microsoft PowerPoint
                    $"powerpnt.exe \"{result.FilePath}\" /s \"{searchTerm}\"",
                    $"\"C:\\Program Files\\Microsoft Office\\root\\Office16\\POWERPNT.EXE\" \"{result.FilePath}\"",
                    $"\"C:\\Program Files (x86)\\Microsoft Office\\root\\Office16\\POWERPNT.EXE\" \"{result.FilePath}\"",
                    // Office 2019/2021 paths
                    $"\"C:\\Program Files\\Microsoft Office\\Office16\\POWERPNT.EXE\" \"{result.FilePath}\"",
                    $"\"C:\\Program Files (x86)\\Microsoft Office\\Office16\\POWERPNT.EXE\" \"{result.FilePath}\"",
                };
                
                foreach (string command in powerpointCommands)
                {
                    try
                    {
                        Process? process = Process.Start(new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = $"/c {command}",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            WindowStyle = ProcessWindowStyle.Hidden
                        });
                        
                        if (process != null)
                        {
                            await Task.Delay(1000);
                            logger.LogDebug("Opened PowerPoint file: {FilePath}", result.FilePath);
                            return true;
                        }
                    }
                    catch
                    {
                        // Try the next command
                        continue;
                    }
                }
            }
            
            // Fall back to normal opening
            Process? fallbackProcess = Process.Start(new ProcessStartInfo
            {
                FileName = result.FilePath,
                UseShellExecute = true
            });
            
            return fallbackProcess != null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to open PowerPoint file with navigation: {FilePath}", result.FilePath);
            
            // Last resort - try basic opening
            try
            {
                Process? basicProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = result.FilePath,
                    UseShellExecute = true
                });
                return basicProcess != null;
            }
            catch
            {
                return false;
            }
        }
    }

    private string? FindMostRelevantSheet(Dictionary<string, object> structuredData, List<string> keywords)
    {
        if (keywords.Count == 0 || structuredData.Count == 0)
            return null;

        var sheetScores = new Dictionary<string, double>();
        
        foreach ((string sheetName, object sheetData) in structuredData)
        {
            // Score based on sheet name relevance
            string lowerSheetName = sheetName.ToLowerInvariant();
            double score = keywords.Where(keyword => lowerSheetName.Contains(keyword)).Sum(keyword => 5.0);

            // Score based on sheet content relevance
            if (sheetData is List<List<string>> rows)
            {
                score += (from row in rows from cell
                    in row where !string.IsNullOrWhiteSpace(cell)
                    select cell.ToLowerInvariant()
                    into lowerCell
                    from keyword in keywords
                    where lowerCell.Contains(keyword)
                    select 1.0).Sum();
            }
            
            sheetScores[sheetName] = score;
        }
        
        // Return the sheet with the highest score, or null if no matches
        KeyValuePair<string, double> bestSheet = sheetScores.OrderByDescending(s => s.Value).FirstOrDefault();
        return bestSheet.Value > 0 ? bestSheet.Key : null;
    }

    private static int CountOccurrences(string text, string substring)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(substring))
            return 0;

        var count = 0;
        var index = 0;
        
        while ((index = text.IndexOf(substring, index, StringComparison.OrdinalIgnoreCase)) != -1)
        {
            count++;
            index += substring.Length;
        }
        
        return count;
    }
    #endregion

    #endregion
}
