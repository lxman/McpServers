# Query Logs

Query logs from Azure Monitor Log Analytics workspace.

## Parameters
- **workspaceId** (string): Log Analytics workspace ID
- **query** (string): KQL query
- **timeRangeHours** (int, optional): Time range in hours (default: 24)
- **useQuickEstimate** (bool, optional): Use quick estimate (default: true)
- **maxResults** (int, optional): Maximum results (default: 1000)

## Returns
JSON object with query results and pagination metadata.

## Example Response
```json
{
  "success": true,
  "result": {
    "tables": [
      {
        "name": "PrimaryResult",
        "columns": ["TimeGenerated", "Message"],
        "rows": [["2025-01-10T12:00:00Z", "Log message"]]
      }
    ],
    "pagination": {
      "totalRows": 150,
      "estimatedCount": 150
    }
  }
}
```
