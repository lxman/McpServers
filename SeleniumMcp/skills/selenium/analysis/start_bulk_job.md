# start_bulk_job

Start a long-running bulk job search that processes jobs in the background. Returns immediately with a job ID that can be used to monitor progress.

**⚠️ IMPORTANT**: This is an asynchronous operation. The tool returns immediately with a job ID. Use `check_job_status` to monitor progress and `get_bulk_job_results` to retrieve final results when complete.

## When to Use

- When you need to process 20+ jobs and don't want to block the conversation
- When scraping operations might take 5-15 minutes
- When you want to monitor progress incrementally without token explosion

## Alternative

For smaller batches (< 20 jobs) or when immediate results are needed, use `bulk_process_jobs` instead (blocks until complete).

## Parameters

- **searchTerm** (string): Job title or keyword to search for
- **location** (string): Geographic location for the job search
- **targetJobs** (int, optional): Target number of jobs to process. Default: 20
- **maxAgeInDays** (int, optional): Maximum age of job postings in days. Default: 30
- **userId** (string, optional): User identifier for tracking. Default: "bulk_user"

## Returns

JSON string containing:
- **success** (boolean): Whether the job was started successfully
- **jobId** (string): Unique identifier for this background job
- **status** (string): Always "started" on success
- **searchTerm** (string): Echo of the search term
- **location** (string): Echo of the location
- **targetJobs** (number): Echo of target job count
- **message** (string): Instructions for monitoring progress

## Automatic Recovery

**All results are automatically saved** to a temporary collection as batches complete. If the job is cancelled, times out, or errors occur, you can recover the partial results:

1. Use `get_temporary_results(sessionId)` to inspect what was saved
2. Use `consolidate_temporary_results(sessionId, userId)` to save to permanent storage

The sessionId is the same as the jobId for async operations.

## Workflow

1. Call `start_bulk_job` to initiate background processing → Get jobId
2. Wait 30-60 seconds, then call `check_job_status(jobId)` to see progress
3. Repeat step 2 until `isComplete` is true (check every 30-60 seconds)
4. Call `get_bulk_job_results(jobId)` to retrieve full job listings
5. (Optional) If interrupted, use `consolidate_temporary_results` to recover partial results

## Example

```json
{
  "success": true,
  "jobId": "a3f8b9c2-1e4d-5a6b-7c8d-9e0f1a2b3c4d",
  "status": "started",
  "searchTerm": "Senior .NET Developer",
  "location": "Remote in USA",
  "targetJobs": 20,
  "message": "Bulk job started. Use check_job_status(jobId='a3f8b9c2-1e4d-5a6b-7c8d-9e0f1a2b3c4d') to monitor progress."
}
```

## See Also

- `check_job_status` - Monitor job progress
- `get_bulk_job_results` - Retrieve final results
- `cancel_job` - Cancel a running job
- `list_bulk_jobs` - List all jobs
- `bulk_process_jobs` - Synchronous alternative (blocks until complete)
