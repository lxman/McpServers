# OCR Image

Performs Optical Character Recognition on an image file to extract text content. Supports common image formats like PNG, JPEG, TIFF, BMP.

## Parameters

- **filePath** (string, required): Full path to the image file to process
- **language** (string, optional): OCR language code (e.g., "eng", "fra", "deu"). Default: "eng"
- **autoRotate** (boolean, required): Automatically rotate image to correct orientation
- **enhanceImage** (boolean, required): Apply image enhancement before OCR for better accuracy

## Returns

Returns a JSON object with OCR results:
```json
{
  "success": true,
  "filePath": "/path/to/image.png",
  "extractedText": "Recognized text from image...",
  "language": "eng",
  "confidence": 0.89,
  "processingTime": 2.1,
  "imageSize": {
    "width": 1920,
    "height": 1080
  }
}
```

## Example

```javascript
// Basic image OCR
ocr_image({
  filePath: "C:\\Images\\screenshot.png",
  autoRotate: true,
  enhanceImage: true
})

// OCR with specific language and no enhancement
ocr_image({
  filePath: "C:\\Images\\german_text.jpg",
  language: "deu",
  autoRotate: false,
  enhanceImage: false
})
```
