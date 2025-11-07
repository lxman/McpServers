# Search Multiple Workspaces with Regex

Search multiple workspaces using regex pattern.

## Parameters
- **workspaceIds** (string): Comma-separated workspace IDs
- **regexPattern** (string): Regex pattern to search
- **timeRangeHours** (int, optional): Time range in hours (default: 24)
- **contextLines** (int, optional): Context lines (default: 2)
- **caseSensitive** (bool, optional): Case sensitive (default: false)
- **maxMatches** (int, optional): Maximum matches (default: 100)
- **maxWorkspaces** (int, optional): Maximum workspaces (default: 5)

## Returns
JSON object with matching log entries across workspaces.

## Example Response
```json
{
  "success": true,
  "matchCount": 12,
  "workspacesSearched": 3,
  "matches": [
    {
      "workspaceId": "workspace-1",
      "timestamp": "2025-01-10T12:00:00Z",
      "message": "Error in application"
    }
  ]
}
```
