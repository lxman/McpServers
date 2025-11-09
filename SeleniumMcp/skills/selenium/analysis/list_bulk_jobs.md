# list_bulk_jobs

List all bulk jobs (active and recently completed) to see what background processing is happening.

## Parameters

- **includeCompleted** (boolean, optional): Whether to include completed/failed/cancelled jobs. Default: true

## Returns

JSON string containing:
- **success** (boolean): Whether the request succeeded
- **count** (int): Number of jobs returned
- **jobs** (array): List of job summaries
  - **jobId** (string): Unique job identifier
  - **status** (string): Current status: "Starting", "Running", "Completed", "Failed", or "Cancelled"
  - **searchTerm** (string): Search term being processed
  - **location** (string): Location being searched
  - **startTime** (datetime): When the job started
  - **endTime** (datetime, nullable): When the job finished (null if still running)
  - **jobsProcessed** (int): Number of jobs processed

## Example Response

```json
{
  "success": true,
  "count": 3,
  "jobs": [
    {
      "jobId": "a3f8b9c2-1e4d-5a6b-7c8d-9e0f1a2b3c4d",
      "status": "Running",
      "searchTerm": "Senior .NET Developer",
      "location": "Remote in USA",
      "startTime": "2025-11-09T14:20:00Z",
      "endTime": null,
      "jobsProcessed": 12
    },
    {
      "jobId": "b4e9c3d2-2f5e-6b7c-8d9e-0f1a2b3c4d5e",
      "status": "Completed",
      "searchTerm": "Principal Engineer",
      "location": "San Francisco, CA",
      "startTime": "2025-11-09T13:45:00Z",
      "endTime": "2025-11-09T13:49:23Z",
      "jobsProcessed": 20
    },
    {
      "jobId": "c5f0d4e3-3g6f-7c8d-9e0f-1a2b3c4d5e6f",
      "status": "Cancelled",
      "searchTerm": "Staff Software Engineer",
      "location": "New York, NY",
      "startTime": "2025-11-09T12:30:00Z",
      "endTime": "2025-11-09T12:33:15Z",
      "jobsProcessed": 8
    }
  ]
}
```

## Use Cases

- Check if any jobs are currently running
- Find the jobId of a recently completed job
- See history of recent bulk processing operations
- Monitor multiple concurrent jobs

## Job Retention

- Active jobs are retained indefinitely
- Completed/failed/cancelled jobs are retained for 1 hour
- System keeps at least the 50 most recent completed jobs
- Old completed jobs are cleaned up every 10 minutes

## Filtering

To see only active jobs (exclude completed):
```
list_bulk_jobs(includeCompleted=false)
```

## See Also

- `start_bulk_job` - Start background job
- `check_job_status` - Check specific job status
- `get_bulk_job_results` - Get full results
- `cancel_job` - Cancel running job
