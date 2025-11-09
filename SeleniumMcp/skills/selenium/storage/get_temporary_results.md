# get_temporary_results

Inspect temporary job results from scraping sessions. Use to check what results are saved before consolidation or to recover from interrupted operations.

## When to Use

- To inspect results from an interrupted or cancelled scraping operation
- To see what was auto-saved before deciding to consolidate
- To check if temporary results exist for a specific session
- To debug or verify the temporary storage system
- To list all pending (unconsolidated) scraping sessions

## How It Works

All scraping operations automatically save results to a temporary collection as they progress. This tool lets you inspect those temporary results before moving them to permanent storage via `consolidate_temporary_results`.

## Parameters

- **sessionId** (string, optional): Filter by specific session ID
  - If provided, returns only results for that session
  - If omitted, returns summary of all unconsolidated sessions
  - Example: `"a1b2c3d4-e5f6-7890-abcd-ef1234567890"`
- **includeConsolidated** (boolean, optional): Include already consolidated results
  - Default: `false` (only show unconsolidated results)
  - Set to `true` to see historical temporary results
  - Example: `true`

## Returns

JSON string containing:
- **success** (boolean): Whether the query completed successfully
- **totalResults** (number): Total number of temporary job listings found
- **sessionCount** (number): Number of distinct sessions found
- **sessions** (array): Summary of each session with:
  - **sessionId**: Unique session identifier
  - **jobCount**: Number of jobs in this session
  - **batches**: Number of batches saved
  - **operationType**: Type of operation (bulk, single_site, multi_site, etc.)
  - **searchTerm**: Search term used
  - **location**: Location searched
  - **consolidated**: Whether already moved to final collection
  - **savedAt**: Timestamp of most recent save
- **jobs** (array, optional): Full job details if specific sessionId requested and count ≤ 50

## Example - All Sessions Summary

```json
{
  "success": true,
  "totalResults": 127,
  "sessionCount": 3,
  "sessions": [
    {
      "sessionId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "jobCount": 47,
      "batches": 5,
      "operationType": "bulk",
      "searchTerm": "Senior .NET Developer",
      "location": "Remote",
      "consolidated": false,
      "savedAt": "2025-01-09T15:30:00Z"
    },
    {
      "sessionId": "b2c3d4e5-f6a7-8901-bcde-f23456789012",
      "jobCount": 52,
      "batches": 6,
      "operationType": "multi_site_combined",
      "searchTerm": "C# Engineer",
      "location": "San Francisco",
      "consolidated": false,
      "savedAt": "2025-01-09T14:15:00Z"
    },
    {
      "sessionId": "c3d4e5f6-a7b8-9012-cdef-345678901234",
      "jobCount": 28,
      "batches": 3,
      "operationType": "single_site",
      "searchTerm": "Software Architect",
      "location": "New York",
      "consolidated": false,
      "savedAt": "2025-01-09T13:00:00Z"
    }
  ]
}
```

## Example - Specific Session with Jobs

```json
{
  "success": true,
  "totalResults": 47,
  "sessionCount": 1,
  "sessions": [
    {
      "sessionId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "jobCount": 47,
      "batches": 5,
      "operationType": "bulk",
      "searchTerm": "Senior .NET Developer",
      "location": "Remote",
      "consolidated": false,
      "savedAt": "2025-01-09T15:30:00Z"
    }
  ],
  "jobs": [
    {
      "sessionId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "batchNumber": 1,
      "consolidated": false,
      "savedAt": "2025-01-09T15:25:00Z",
      "job": {
        "title": "Senior .NET Developer",
        "company": "TechCorp",
        "location": "Remote",
        "matchScore": 87.5,
        "url": "https://example.com/job/123"
      }
    }
  ]
}
```

## Workflow Example

```
1. Scraping operation starts → auto-saves batches to temp collection
2. Operation is interrupted (timeout, cancellation, crash)
3. Call get_temporary_results() to see all pending sessions
4. Call get_temporary_results(sessionId) to inspect specific session
5. Call consolidate_temporary_results(sessionId, userId) to save permanently
```

## Notes

- Only returns up to 100 most recent temporary results when no sessionId specified
- Full job listings only included when sessionId specified AND count ≤ 50
- Otherwise returns session summaries only to avoid token explosion
- Temporary results older than 24 hours or already consolidated are cleaned up automatically
- Results are ordered by most recent first
