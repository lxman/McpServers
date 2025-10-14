using DocumentServer.Models.Requests;
using DocumentServer.Services.Ocr;
using DocumentServer.Services.Ocr.Models;
using Microsoft.AspNetCore.Mvc;

namespace DocumentServer.Controllers;

/// <summary>
/// Controller for OCR (Optical Character Recognition) operations
/// </summary>
[ApiController]
[Route("api/ocr")]
public class OcrController(ILogger<OcrController> logger, OcrService ocrService) : ControllerBase
{
    /// <summary>
    /// Get OCR service status
    /// </summary>
    /// <returns>OCR availability status</returns>
    [HttpGet("status")]
    [ProducesResponseType(typeof(Dictionary<string, object>), StatusCodes.Status200OK)]
    public IActionResult GetStatus()
    {
        logger.LogDebug("Getting OCR service status");

        var status = new Dictionary<string, object>
        {
            ["available"] = ocrService.IsAvailable,
            ["engine"] = "Tesseract",
            ["message"] = ocrService.IsAvailable
                ? "OCR service is available"
                : "OCR service is not available - Tesseract not configured"
        };

        logger.LogInformation("OCR service status: Available={Available}", ocrService.IsAvailable);

        return Ok(status);
    }

    /// <summary>
    /// Check if a PDF is scanned (requires OCR)
    /// </summary>
    /// <param name="filePath">Path to the PDF file</param>
    /// <param name="password">Optional password for encrypted PDFs</param>
    /// <returns>Whether the PDF is scanned</returns>
    [HttpGet("check-scanned")]
    [ProducesResponseType(typeof(Dictionary<string, object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult CheckIfPdfScanned([FromQuery] string filePath, [FromQuery] string? password = null)
    {
        logger.LogInformation("Checking if PDF is scanned: {FilePath}", filePath);

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return BadRequest(new { Error = "File path is required" });
        }

        if (!System.IO.File.Exists(filePath))
        {
            logger.LogWarning("File not found: {FilePath}", filePath);
            return BadRequest(new { Error = "File not found" });
        }

        try
        {
            bool isScanned = ocrService.IsPdfScanned(filePath, password);

            var response = new Dictionary<string, object>
            {
                ["filePath"] = filePath,
                ["isScanned"] = isScanned,
                ["requiresOcr"] = isScanned,
                ["message"] = isScanned
                    ? "PDF contains scanned images and requires OCR"
                    : "PDF contains extractable text"
            };

            logger.LogInformation("PDF scan check complete: {FilePath}, IsScanned={IsScanned}",
                filePath, isScanned);

            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking if PDF is scanned: {FilePath}", filePath);
            return BadRequest(new { Error = ex.Message });
        }
    }

    /// <summary>
    /// Extract text from a scanned PDF using OCR
    /// </summary>
    /// <param name="request">OCR parameters</param>
    /// <returns>OCR result with extracted text</returns>
    [HttpPost("pdf")]
    [ProducesResponseType(typeof(OcrResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ExtractTextFromPdf([FromBody] OcrPdfRequest request)
    {
        logger.LogInformation("Extracting text from scanned PDF: {FilePath}", request.FilePath);

        if (!ocrService.IsAvailable)
        {
            logger.LogWarning("OCR service not available");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                Error = "OCR service is not available",
                Message = "Tesseract OCR engine is not configured"
            });
        }

        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            return BadRequest(new { Error = "File path is required" });
        }

        if (!System.IO.File.Exists(request.FilePath))
        {
            logger.LogWarning("File not found: {FilePath}", request.FilePath);
            return BadRequest(new { Error = "File not found" });
        }

        try
        {
            OcrResult result = await ocrService.ExtractTextFromScannedPdf(
                request.FilePath,
                request.Password);

            if (result.Success)
            {
                logger.LogInformation(
                    "OCR extraction complete: {FilePath}, Extracted {Length} characters from {Pages} pages",
                    request.FilePath, result.ExtractedText.Length, result.PagesProcessed);
            }
            else
            {
                logger.LogWarning("OCR extraction failed: {FilePath}, Error: {Error}",
                    request.FilePath, result.ErrorMessage);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during OCR extraction from PDF: {FilePath}", request.FilePath);
            return BadRequest(new { Error = ex.Message });
        }
    }

    /// <summary>
    /// Extract text from an image file using OCR
    /// </summary>
    /// <param name="request">OCR parameters</param>
    /// <returns>OCR result with extracted text</returns>
    [HttpPost("image")]
    [ProducesResponseType(typeof(OcrResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ExtractTextFromImage([FromBody] OcrImageRequest request)
    {
        logger.LogInformation("Extracting text from image: {FilePath}", request.FilePath);

        if (!ocrService.IsAvailable)
        {
            logger.LogWarning("OCR service not available");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                Error = "OCR service is not available",
                Message = "Tesseract OCR engine is not configured"
            });
        }

        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            return BadRequest(new { Error = "File path is required" });
        }

        if (!System.IO.File.Exists(request.FilePath))
        {
            logger.LogWarning("File not found: {FilePath}", request.FilePath);
            return BadRequest(new { Error = "File not found" });
        }

        try
        {
            OcrResult result = await ocrService.ExtractTextFromImage(
                request.FilePath,
                request.PreprocessImage);

            if (result.Success)
            {
                logger.LogInformation("OCR extraction complete: {FilePath}, Extracted {Length} characters",
                    request.FilePath, result.ExtractedText.Length);
            }
            else
            {
                logger.LogWarning("OCR extraction failed: {FilePath}, Error: {Error}",
                    request.FilePath, result.ErrorMessage);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during OCR extraction from image: {FilePath}", request.FilePath);
            return BadRequest(new { Error = ex.Message });
        }
    }
}