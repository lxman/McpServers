# Get OCR Status

Retrieves the current status and configuration of the OCR (Optical Character Recognition) engine, including available languages and processing capabilities.

## Parameters

None

## Returns

Returns a JSON object with OCR engine status:
```json
{
  "success": true,
  "available": true,
  "version": "5.0.0",
  "languages": ["eng", "fra", "deu", "spa", "ita"],
  "defaultLanguage": "eng",
  "capabilities": {
    "autoRotate": true,
    "imageEnhancement": true,
    "multiLanguage": true
  },
  "activeProcesses": 0
}
```

## Example

```javascript
get_ocr_status()
```
