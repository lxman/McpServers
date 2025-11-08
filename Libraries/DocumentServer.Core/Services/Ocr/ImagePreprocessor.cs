using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace DocumentServer.Core.Services.Ocr;

/// <summary>
/// Provides image preprocessing and enhancement services for OCR
/// </summary>
public class ImagePreprocessor(ILogger<ImagePreprocessor> logger)
{
    /// <summary>
    /// Enhance image using SixLabors ImageSharp for better OCR accuracy
    /// </summary>
    /// <param name="imageBytes">Original image bytes</param>
    /// <param name="scaleFactor">Scaling factor for image size (default: 2x)</param>
    /// <returns>Enhanced image bytes optimized for OCR</returns>
    public byte[] EnhanceImageForOcr(byte[] imageBytes, int scaleFactor = 2)
    {
        try
        {
            logger.LogDebug("Enhancing image for OCR (scale factor: {ScaleFactor}x)", scaleFactor);

            using var inputStream = new MemoryStream(imageBytes);
            using var image = Image.Load(inputStream);

            // Calculate target dimensions
            var targetWidth = image.Width * scaleFactor;
            var targetHeight = image.Height * scaleFactor;
            
            logger.LogDebug("Original size: {Width}x{Height}, Target size: {TargetWidth}x{TargetHeight}",
                image.Width, image.Height, targetWidth, targetHeight);

            // Apply image enhancements for better OCR
            image.Mutate(x => x
                .Resize(new ResizeOptions
                {
                    Size = new Size(targetWidth, targetHeight),
                    Mode = ResizeMode.BoxPad
                })
                .Grayscale()                    // Convert to grayscale
                .Contrast(1.2f)                 // Increase contrast
                .BinaryThreshold(0.5f)          // Apply binary threshold for cleaner text
                .GaussianSharpen(1.0f)          // Sharpen edges
            );
            
            using var outputStream = new MemoryStream();
            image.Save(outputStream, new PngEncoder());
            var enhancedBytes = outputStream.ToArray();

            logger.LogDebug("Image enhanced successfully. Original: {OriginalSize} bytes, Enhanced: {EnhancedSize} bytes",
                imageBytes.Length, enhancedBytes.Length);

            return enhancedBytes;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to enhance image, using original");
            return imageBytes;
        }
    }

    /// <summary>
    /// Apply custom enhancement pipeline to an image
    /// </summary>
    /// <param name="imageBytes">Original image bytes</param>
    /// <param name="operations">Custom enhancement operations</param>
    /// <returns>Enhanced image bytes</returns>
    public byte[] ApplyCustomEnhancements(byte[] imageBytes, Action<IImageProcessingContext> operations)
    {
        try
        {
            logger.LogDebug("Applying custom image enhancements for OCR");

            using var inputStream = new MemoryStream(imageBytes);
            using var image = Image.Load(inputStream);

            image.Mutate(operations);
            
            using var outputStream = new MemoryStream();
            image.Save(outputStream, new PngEncoder());
            var enhancedBytes = outputStream.ToArray();

            logger.LogDebug("Custom enhancements applied successfully");
            return enhancedBytes;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to apply custom enhancements, using original");
            return imageBytes;
        }
    }

    /// <summary>
    /// Get image dimensions without loading the full image
    /// </summary>
    public (int Width, int Height)? GetImageDimensions(byte[] imageBytes)
    {
        try
        {
            using var inputStream = new MemoryStream(imageBytes);
            var imageInfo = Image.Identify(inputStream);
            return (imageInfo.Width, imageInfo.Height);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get image dimensions");
            return null;
        }
    }
}
