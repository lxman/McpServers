# get_stored_jobs

Retrieve stored jobs from MongoDB with comprehensive filtering and pagination support. Designed to handle large result sets efficiently.

## Parameters

- **userId** (string, required): User identifier to retrieve jobs for
- **sitesJson** (string, optional): JSON array of JobSite enum integers to filter by source sites. Example: `"[2, 11]"` for Dice and BuiltIn
- **fromDate** (string, optional): Filter jobs posted on or after this date (ISO 8601 format)
- **toDate** (string, optional): Filter jobs posted on or before this date (ISO 8601 format)
- **isRemote** (bool, optional): Filter for remote jobs only
- **minMatchScore** (double, optional): Minimum match score threshold (0-100). Recommended: 70+ for quality matches
- **isApplied** (bool, optional): Filter by application status (true = already applied, false = not applied)
- **requiredSkillsJson** (string, optional): JSON array of required skills. Example: `"[\"C#\", \".NET\"]"`
- **limit** (int, optional): Maximum number of results to return per request. Default: 100. Use lower values to avoid token limits.
- **skip** (int, optional): Number of results to skip for pagination. Default: 0

## Returns

JSON string containing:
- **success** (boolean): Whether the retrieval operation completed successfully
- **userId** (string): The user ID that was queried
- **filters** (object): Applied filters including sites, dates, match score, etc.
- **totalCount** (number): Total number of jobs matching the filter criteria
- **returnedCount** (number): Number of jobs returned in this paginated response
- **skip** (number): Number of results that were skipped
- **limit** (number): Maximum results requested per page
- **hasMore** (boolean): Whether there are more results available (true if totalCount > skip + limit)
- **jobs** (array): Array of EnhancedJobListing objects from storage

## Example

```json
{
  "success": true,
  "userId": "test_user",
  "filters": {
    "Sites": [],
    "FromDate": null,
    "ToDate": null,
    "IsRemote": null,
    "MinMatchScore": 70.0,
    "IsApplied": null,
    "RequiredSkills": []
  },
  "totalCount": 287,
  "returnedCount": 100,
  "skip": 0,
  "limit": 100,
  "hasMore": true,
  "jobs": [
    {
      "Id": "507f1f77bcf86cd799439011",
      "Title": "Senior .NET Developer",
      "Company": "TechCorp",
      "Location": "Remote",
      "Url": "https://example.com/job/123",
      "SourceSite": 2,
      "Salary": "$150,000 - $180,000",
      "MatchScore": 92.5,
      "DatePosted": "2025-11-05T00:00:00Z",
      "Technologies": ["C#", ".NET", "Azure"]
    }
  ]
}
```

## Usage Notes

- **Default limit is 100 jobs** to prevent token limit errors with large datasets
- Use **minMatchScore >= 70** for high-quality matches
- For pagination, use: `skip=0, limit=100` for first page, then `skip=100, limit=100` for second page, etc.
- Check `hasMore` field to determine if additional pages exist
- Consider applying filters (minMatchScore, isRemote, dates) to reduce result set size before paginating
