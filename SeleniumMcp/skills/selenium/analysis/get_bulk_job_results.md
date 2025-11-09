# get_bulk_job_results

Get the full results of a completed bulk job. This tool returns the complete list of job listings with all details.

**⚠️ IMPORTANT**: Only call this tool ONCE when the job is complete. This returns the full job listings payload (~150-300KB) and should only be retrieved when ready to process results.

## When to Use

- After `check_job_status` returns `isComplete: true`
- When you're ready to process, analyze, or save the job listings
- Only call ONCE to avoid duplicate large payloads

## Parameters

- **jobId** (string, required): The job ID from `start_bulk_job`

## Returns

JSON string containing:
- **success** (boolean): Whether results were retrieved
- **jobId** (string): The job identifier
- **status** (string): Final status: "Completed", "Failed", or "Cancelled"
- **result** (object): Complete BulkProcessingResult with all job listings
  - **processedJobs** (array): Full list of EnhancedJobListing objects
  - **startTime** (datetime): When processing started
  - **endTime** (datetime): When processing finished
  - **totalDuration** (timespan): Total processing time
  - **pagesProcessed** (int): Number of pages scraped
  - **highPriorityCount** (int): Jobs scoring 80%+
  - **applicationReadyCount** (int): Jobs scoring 60-79%
  - **considerCount** (int): Jobs scoring 40-59%
  - **lowPriorityCount** (int): Jobs scoring < 40%
  - **averageScore** (number): Average match score
  - **jobsPerMinute** (number): Processing rate
  - **errors** (array): Any errors encountered

## Error Responses

### Job Not Found
```json
{
  "success": false,
  "message": "Job abc123 not found"
}
```

### Job Still Running
```json
{
  "success": false,
  "message": "Job abc123 is still running. Use check_job_status to monitor progress.",
  "status": "Running",
  "jobsProcessed": 12,
  "summary": { ... }
}
```

### Results No Longer Available
```json
{
  "success": false,
  "message": "Job abc123 completed but results are no longer available"
}
```

## Example Success Response

```json
{
  "success": true,
  "jobId": "a3f8b9c2-1e4d-5a6b-7c8d-9e0f1a2b3c4d",
  "status": "Completed",
  "result": {
    "processedJobs": [
      {
        "id": "507f1f77bcf86cd799439011",
        "title": "Senior .NET Developer",
        "company": "TechCorp",
        "location": "Remote",
        "salary": "$140,000 - $180,000",
        "matchScore": 85.5,
        "sourceSite": 11,
        "datePosted": "2025-11-05T10:30:00Z",
        "scrapedAt": "2025-11-09T14:23:45Z",
        "url": "https://example.com/job/123",
        "description": "...",
        "requiredSkills": [".NET Core", "C#", "Azure"],
        "isRemote": true,
        "notes": "Bulk Score: 85.5% (Page: 2, Batch: 8)"
      }
      // ... 19 more jobs
    ],
    "startTime": "2025-11-09T14:20:00Z",
    "endTime": "2025-11-09T14:24:03Z",
    "totalDuration": "00:04:03",
    "pagesProcessed": 5,
    "highPriorityCount": 3,
    "applicationReadyCount": 7,
    "considerCount": 6,
    "lowPriorityCount": 4,
    "averageScore": 68.5,
    "jobsPerMinute": 4.92,
    "errors": []
  }
}
```

## Next Steps

After retrieving results:
1. Process/analyze the job listings as needed
2. Call `save_jobs(jobs, userId)` to persist to database
3. Optionally use analysis tools:
   - `smart_deduplication` - Remove duplicates
   - `categorize_applications` - Categorize by priority
   - `market_intelligence` - Generate market insights

## See Also

- `start_bulk_job` - Start background job
- `check_job_status` - Monitor progress
- `save_jobs` - Save results to database
- `cancel_job` - Cancel running job
