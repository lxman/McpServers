using System.ComponentModel;
using System.Text.Json;
using Mcp.Common.Core;
using DocumentServer.Core.Services.Ocr;
using DocumentServer.Core.Services.Ocr.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DocumentMcp.McpTools;

/// <summary>
/// MCP tools for OCR operations
/// </summary>
[McpServerToolType]
public class OcrTools(
    OcrService ocrService,
    ILogger<OcrTools> logger)
{
    [McpServerTool, DisplayName("get_ocr_status")]
    [Description("Get OCR service status and configuration. See skills/document/ocr/get-status.md only when using this tool")]
    public string GetOcrStatus()
    {
        try
        {
            logger.LogDebug("Getting OCR status");

            return JsonSerializer.Serialize(new
            {
                success = true,
                status = "operational",
                engine = "Tesseract",
                version = "5.3.0",
                supportedLanguages = (string[])["eng", "fra", "deu", "spa", "ita", "por", "rus", "chi_sim", "jpn"],
                defaultLanguage = "eng",
                capabilities = new
                {
                    pdfOcr = true,
                    imageOcr = true,
                    multiLanguage = true,
                    autoRotation = true,
                    textDetection = true
                }
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting OCR status");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("check_scanned_pdf")]
    [Description("Check if a PDF is scanned (image-based) or contains searchable text. See skills/document/ocr/check-scanned.md only when using this tool")]
    public string CheckScannedPdf(string filePath)
    {
        try
        {
            logger.LogDebug("Checking if PDF is scanned: {FilePath}", filePath);

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return JsonSerializer.Serialize(new { success = false, error = "File path is required" }, SerializerOptions.JsonOptionsIndented);
            }

            if (!File.Exists(filePath))
            {
                return JsonSerializer.Serialize(new { success = false, error = "File not found" }, SerializerOptions.JsonOptionsIndented);
            }

            if (!filePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return JsonSerializer.Serialize(new { success = false, error = "File must be a PDF" }, SerializerOptions.JsonOptionsIndented);
            }

            bool isScanned = ocrService.IsPdfScanned(filePath, null);

            return JsonSerializer.Serialize(new
            {
                success = true,
                filePath,
                isScanned,
                hasText = !isScanned,
                recommendation = isScanned
                    ? "PDF is image-based, OCR is recommended to extract text"
                    : "PDF contains searchable text, OCR is not needed"
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking scanned PDF: {FilePath}", filePath);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("ocr_pdf")]
    [Description("Perform OCR on a PDF document. See skills/document/ocr/ocr-pdf.md only when using this tool")]
    public async Task<string> OcrPdf(
        string filePath,
        string? language = null,
        bool autoRotate = true,
        bool enhanceImage = false)
    {
        try
        {
            logger.LogDebug("Performing OCR on PDF: {FilePath}, Language: {Language}", filePath, language ?? "eng");

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return JsonSerializer.Serialize(new { success = false, error = "File path is required" }, SerializerOptions.JsonOptionsIndented);
            }

            if (!File.Exists(filePath))
            {
                return JsonSerializer.Serialize(new { success = false, error = "File not found" }, SerializerOptions.JsonOptionsIndented);
            }

            if (!filePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return JsonSerializer.Serialize(new { success = false, error = "File must be a PDF" }, SerializerOptions.JsonOptionsIndented);
            }

            OcrResult result = await ocrService.ExtractTextFromScannedPdf(filePath, null);

            if (!result.Success)
            {
                return JsonSerializer.Serialize(new { success = false, error = result.ErrorMessage }, SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                filePath,
                text = result.ExtractedText,
                pagesProcessed = result.PagesProcessed,
                pagesWithErrors = result.PagesWithErrors,
                warnings = result.Warnings,
                metadata = result.Metadata
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error performing OCR on PDF: {FilePath}", filePath);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("ocr_image")]
    [Description("Perform OCR on an image file. See skills/document/ocr/ocr-image.md only when using this tool")]
    public async Task<string> OcrImage(
        string filePath,
        string? language = null,
        bool autoRotate = true,
        bool enhanceImage = false)
    {
        try
        {
            logger.LogDebug("Performing OCR on image: {FilePath}, Language: {Language}", filePath, language ?? "eng");

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return JsonSerializer.Serialize(new { success = false, error = "File path is required" }, SerializerOptions.JsonOptionsIndented);
            }

            if (!File.Exists(filePath))
            {
                return JsonSerializer.Serialize(new { success = false, error = "File not found" }, SerializerOptions.JsonOptionsIndented);
            }

            string[] supportedFormats = [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tiff", ".tif"];
            string extension = Path.GetExtension(filePath).ToLower();
            if (!supportedFormats.Contains(extension))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Unsupported image format. Supported formats: {string.Join(", ", supportedFormats)}"
                }, SerializerOptions.JsonOptionsIndented);
            }

            OcrResult result = await ocrService.ExtractTextFromImage(filePath, enhanceImage);

            if (!result.Success)
            {
                return JsonSerializer.Serialize(new { success = false, error = result.ErrorMessage }, SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                filePath,
                text = result.ExtractedText,
                confidence = result.Confidence,
                pagesProcessed = result.PagesProcessed,
                metadata = result.Metadata
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error performing OCR on image: {FilePath}", filePath);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }
}