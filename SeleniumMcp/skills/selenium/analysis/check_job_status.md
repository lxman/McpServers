# check_job_status

Check the current status and progress of a background bulk job. Returns lightweight summary statistics to avoid token explosion.

**⚠️ TOKEN FRIENDLY**: This tool returns only summary statistics (~500 bytes), NOT full job listings. Use `get_bulk_job_results` to retrieve complete results when job is finished.

## Parameters

- **jobId** (string, required): The job ID returned by `start_bulk_job`

## Returns

JSON string containing:
- **success** (boolean): Whether the job was found
- **jobId** (string): The job identifier
- **status** (string): Current status: "starting", "running", "completed", "failed", or "cancelled"
- **progressMessage** (string): Human-readable progress description
- **searchTerm** (string): The search term being processed
- **location** (string): The location being searched
- **jobsProcessed** (int): Number of jobs processed so far
- **currentBatch** (int): Current batch number
- **totalBatches** (int): Estimated total batches
- **elapsedSeconds** (int): Time elapsed since job started
- **isComplete** (boolean): Whether the job has finished (completed, failed, or cancelled)
- **summary** (object, optional): Lightweight statistics (when available)
  - **jobsProcessed** (int): Jobs processed
  - **averageScore** (number): Average match score
  - **highPriorityCount** (int): Jobs scoring 90-100
  - **greatCount** (int): Jobs scoring 75-89
  - **goodCount** (int): Jobs scoring 60-74
  - **fairCount** (int): Jobs scoring < 60
  - **errorCount** (int): Number of errors
  - **pagesProcessed** (int): Pages scraped
  - **elapsedSeconds** (number): Time elapsed
- **message** (string): Status message

## Polling Strategy

**RECOMMENDED**: Poll every 30-60 seconds
- ❌ DON'T poll every few seconds (wastes tokens)
- ✅ DO wait 30-60 seconds between checks
- ✅ DO stop polling once `isComplete` is true

## Example Response (Running)

```json
{
  "success": true,
  "jobId": "a3f8b9c2-1e4d-5a6b-7c8d-9e0f1a2b3c4d",
  "status": "Running",
  "progressMessage": "Batch 3/5: 12/20 jobs, avg score: 72.3",
  "searchTerm": "Senior .NET Developer",
  "location": "Remote in USA",
  "jobsProcessed": 12,
  "currentBatch": 3,
  "totalBatches": 5,
  "elapsedSeconds": 87,
  "isComplete": false,
  "summary": {
    "jobsProcessed": 12,
    "averageScore": 72.3,
    "highPriorityCount": 2,
    "greatCount": 4,
    "goodCount": 5,
    "fairCount": 1,
    "errorCount": 0,
    "pagesProcessed": 3,
    "elapsedSeconds": 87
  },
  "message": "Job running: Batch 3/5: 12/20 jobs, avg score: 72.3"
}
```

## Example Response (Completed)

```json
{
  "success": true,
  "jobId": "a3f8b9c2-1e4d-5a6b-7c8d-9e0f1a2b3c4d",
  "status": "Completed",
  "progressMessage": "Completed: 20 jobs processed",
  "searchTerm": "Senior .NET Developer",
  "location": "Remote in USA",
  "jobsProcessed": 20,
  "currentBatch": 5,
  "totalBatches": 5,
  "elapsedSeconds": 243,
  "isComplete": true,
  "summary": {
    "jobsProcessed": 20,
    "averageScore": 68.5,
    "highPriorityCount": 3,
    "greatCount": 7,
    "goodCount": 6,
    "fairCount": 4,
    "errorCount": 0,
    "pagesProcessed": 5,
    "elapsedSeconds": 243
  },
  "message": "Job completed: Completed: 20 jobs processed"
}
```

## Next Steps

When `isComplete` is true:
1. Call `get_bulk_job_results(jobId)` to retrieve full job listings
2. Optionally call `save_jobs` to persist results to database

## See Also

- `start_bulk_job` - Start background job
- `get_bulk_job_results` - Retrieve full results when complete
- `cancel_job` - Cancel running job
- `list_bulk_jobs` - List all jobs
