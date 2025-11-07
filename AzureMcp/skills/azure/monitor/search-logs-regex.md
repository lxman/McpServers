# Search Logs with Regex

Search logs using regex pattern.

## Parameters
- **workspaceId** (string): Log Analytics workspace ID
- **regexPattern** (string): Regex pattern to search
- **timeRangeHours** (int, optional): Time range in hours (default: 24)
- **contextLines** (int, optional): Context lines (default: 3)
- **caseSensitive** (bool, optional): Case sensitive (default: false)
- **maxMatches** (int, optional): Maximum matches (default: 100)

## Returns
JSON object with matching log entries.

## Example Response
```json
{
  "success": true,
  "matchCount": 5,
  "matches": [
    {
      "timestamp": "2025-01-10T12:00:00Z",
      "message": "Error in application",
      "contextBefore": ["Previous line"],
      "contextAfter": ["Next line"]
    }
  ]
}
```
