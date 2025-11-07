# OCR PDF

Performs Optical Character Recognition on a PDF document to extract text from scanned pages or images. Creates a searchable PDF with recognized text.

## Parameters

- **filePath** (string, required): Full path to the PDF file to process
- **language** (string, optional): OCR language code (e.g., "eng", "fra", "deu"). Default: "eng"
- **autoRotate** (boolean, required): Automatically rotate pages to correct orientation
- **enhanceImage** (boolean, required): Apply image enhancement before OCR for better accuracy

## Returns

Returns a JSON object with OCR results:
```json
{
  "success": true,
  "filePath": "/path/to/document.pdf",
  "processedPages": 10,
  "extractedText": "Recognized text content...",
  "language": "eng",
  "confidence": 0.92,
  "processingTime": 15.3,
  "outputPath": "/path/to/document_ocr.pdf"
}
```

## Example

```javascript
// Basic OCR with English
ocr_pdf({
  filePath: "C:\\Documents\\scanned.pdf",
  autoRotate: true,
  enhanceImage: true
})

// OCR with specific language
ocr_pdf({
  filePath: "C:\\Documents\\french_doc.pdf",
  language: "fra",
  autoRotate: true,
  enhanceImage: false
})
```
