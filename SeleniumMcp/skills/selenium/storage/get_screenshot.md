# get_screenshot

Retrieve screenshot data for a specific job listing from MongoDB storage.

## Parameters

- **url** (string): The URL of the job listing to retrieve screenshot for
- **userId** (string): User identifier for tracking and access control

## Returns

JSON string containing:
- **success** (boolean): Whether the screenshot retrieval completed successfully
- **jobId** (string): The job ID associated with the screenshot
- **screenshotData** (string): Image data in requested format (base64 encoded or URL)
- **format** (string): Format of the returned screenshot
- **dimensions** (object): Width and height of the screenshot
  - **width** (number): Image width in pixels
  - **height** (number): Image height in pixels
- **captureTime** (string): Timestamp when the screenshot was captured
- **error** (string, optional): Error message if screenshot not found or failed to retrieve

## Example

```json
{
  "success": true,
  "jobId": "507f1f77bcf86cd799439011",
  "screenshotData": "iVBORw0KGgoAAAANSUhEUgAAAAUA...",
  "format": "base64",
  "dimensions": {
    "width": 1280,
    "height": 1024
  },
  "captureTime": "2025-11-05T14:30:00Z",
  "error": null
}
```
