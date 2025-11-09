# bulk_process_jobs

Process jobs in bulk with intelligent scoring based on a 50-year .NET developer profile and preferences.

**⚠️ SYNCHRONOUS OPERATION**: This tool blocks until all jobs are processed (2-6 minutes). For better token efficiency and non-blocking operation, use the async workflow instead:
1. `start_bulk_job` - Start background processing
2. `check_job_status` - Monitor progress (lightweight)
3. `get_bulk_job_results` - Retrieve final results when complete

## Parameters

- **searchTerm** (string): Job title or keyword to search for
- **location** (string): Geographic location for the job search
- **targetJobs** (int, optional): Target number of jobs to process. Default: 20

## Returns

JSON string containing:
- **success** (boolean): Whether the bulk processing completed successfully
- **jobsProcessed** (number): Total number of jobs processed
- **averageScore** (number): Average match score across all jobs (0-100)
- **scoreDistribution** (object): Distribution of scores
  - **excellent** (number): Jobs scoring 90-100
  - **great** (number): Jobs scoring 75-89
  - **good** (number): Jobs scoring 60-74
  - **fair** (number): Jobs scoring below 60
- **topMatches** (array): Top 10 best matching jobs
- **scoredJobs** (number): Number of jobs with scores stored
- **duration** (number): Time taken in seconds
- **errors** (array, optional): List of any errors encountered

## Example

```json
{
  "success": true,
  "jobsProcessed": 142,
  "averageScore": 76.8,
  "scoreDistribution": {
    "excellent": 32,
    "great": 54,
    "good": 38,
    "fair": 18
  },
  "topMatches": [
    {
      "id": "507f1f77bcf86cd799439011",
      "title": "Distinguished Engineer - .NET Platform",
      "company": "MegaTech Corp",
      "score": 99,
      "location": "Remote",
      "salary": "$250,000 - $300,000"
    }
  ],
  "scoredJobs": 142,
  "duration": 45.2,
  "errors": []
}
```
