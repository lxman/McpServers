# cancel_job

Cancel a running bulk job and return any partial results collected so far.

## When to Use

- When you want to stop a long-running job early
- When the job is taking too long or you want to adjust search parameters
- To retrieve partial results before the full target is reached

## Parameters

- **jobId** (string, required): The job ID from `start_bulk_job`

## Returns

JSON string containing:
- **success** (boolean): Whether cancellation succeeded
- **message** (string): Cancellation result message
- **status** (string): Job status after cancellation
- **jobsProcessed** (int): Number of jobs processed before cancellation
- **partialResults** (object, optional): BulkProcessingResult with jobs collected so far
- **hasResults** (boolean): Whether any jobs were collected

## Cancellation Process

1. Sends cancellation signal to background processor
2. Waits 1 second for graceful shutdown
3. Returns partial results immediately
4. Background task completes cleanup

## Example Success Response

```json
{
  "success": true,
  "message": "Job a3f8b9c2-1e4d-5a6b-7c8d-9e0f1a2b3c4d cancellation requested. 12 jobs processed before cancellation.",
  "status": "Cancelled",
  "jobsProcessed": 12,
  "partialResults": {
    "processedJobs": [
      {
        "id": "507f1f77bcf86cd799439011",
        "title": "Senior .NET Developer",
        "company": "TechCorp",
        "matchScore": 85.5,
        // ... full job details
      }
      // ... 11 more jobs
    ],
    "startTime": "2025-11-09T14:20:00Z",
    "endTime": "2025-11-09T14:22:30Z",
    "totalDuration": "00:02:30",
    "pagesProcessed": 3,
    "averageScore": 72.3,
    "errors": []
  },
  "hasResults": true
}
```

## Error Responses

### Job Not Found
```json
{
  "success": false,
  "message": "Job abc123 not found"
}
```

### Job Already Complete
```json
{
  "success": false,
  "message": "Job abc123 is already completed",
  "status": "Completed",
  "partialResults": { /* full results */ }
}
```

## Next Steps

After cancellation:
1. Review partial results to see what was collected
2. Optionally save partial results: `save_jobs(partialResults.processedJobs, userId)`
3. Start a new job with adjusted parameters if needed

## See Also

- `start_bulk_job` - Start background job
- `check_job_status` - Monitor job progress
- `get_bulk_job_results` - Get full results of completed jobs
- `list_bulk_jobs` - List all jobs
