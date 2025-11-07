# fetch_jobs_by_ids

Fetch specific jobs by their IDs from storage with optional filtering and enrichment.

## Parameters

- **jobIdsJson** (string): JSON array of job IDs to fetch
  - Example: `["job123", "job456", "job789"]`
- **userId** (string, optional): User identifier for tracking. Default: "default_user"

## Returns

JSON string containing:
- **success** (boolean): Whether the fetch operation completed successfully
- **requestedCount** (number): Number of jobs requested
- **foundCount** (number): Number of jobs found
- **notFoundIds** (array, optional): IDs that were not found in storage
- **jobs** (array): Array of fetched job objects with requested details
  - **id** (string): MongoDB job ID
  - **title** (string): Job title
  - **company** (string): Company name
  - **location** (string): Job location
  - **url** (string): Job listing URL
  - **salary** (string, optional): Salary information
  - **description** (string): Full job description
  - **source** (string): Source where job was found
  - **postedDate** (string): When job was posted
  - **matchScore** (number, optional): If included
  - **applicationStatus** (string, optional): Current application status
  - **screenshotUrl** (string, optional): URL to screenshot if included
- **duration** (number): Time taken in seconds
- **errors** (array, optional): List of any errors encountered

## Example

```json
{
  "success": true,
  "requestedCount": 3,
  "foundCount": 3,
  "notFoundIds": [],
  "jobs": [
    {
      "id": "507f1f77bcf86cd799439011",
      "title": "Senior .NET Developer",
      "company": "TechCorp",
      "location": "San Francisco, CA",
      "url": "https://example.com/jobs/123",
      "salary": "$150,000 - $180,000",
      "description": "We are seeking a senior .NET developer...",
      "source": "Dice",
      "postedDate": "2025-11-04T08:00:00Z",
      "matchScore": 89,
      "applicationStatus": "applied",
      "screenshotUrl": "mongodb://screenshots/507f1f77bcf86cd799439011.png"
    }
  ],
  "duration": 2.1,
  "errors": []
}
```
