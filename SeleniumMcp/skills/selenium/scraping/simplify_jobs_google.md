# simplify_jobs_google

Use Google Custom Search API to discover SimplifyJobs listings and automatically fetch detailed job information.

## Parameters

- **searchTerm** (string): Job title or keyword to search for
- **location** (string): Geographic location for the job search
- **maxResults** (int, optional): Maximum number of results to return. Default: 20
- **userId** (string, optional): User identifier for tracking. Default: "default_user"

## Returns

JSON string containing:
- **success** (boolean): Whether the operation completed successfully
- **discoveredJobs** (array): Array of SimplifyJobs listings discovered through Google search
- **totalDiscovered** (number): Total number of jobs discovered
- **fetched** (number): Number of jobs fetched with full details
- **duplicatesRemoved** (number): Number of duplicate entries removed
- **jobDetails** (array, optional): Full job details if autoFetch is enabled
- **duration** (number): Time taken to complete operation in seconds
- **errors** (array, optional): List of any errors encountered

## Example

```json
{
  "success": true,
  "discoveredJobs": [
    {
      "id": "simplify_google_456",
      "title": ".NET Backend Developer",
      "company": "CloudTech Solutions",
      "location": "Remote",
      "url": "https://simplify.jobs/p/cloudtech-solutions-dotnet-backend-developer",
      "source": "SimplifyJobs",
      "salary": "$120,000 - $160,000",
      "description": "Join our growing cloud platform team..."
    }
  ],
  "totalDiscovered": 28,
  "fetched": 28,
  "duplicatesRemoved": 3,
  "jobDetails": [
    {
      "id": "simplify_google_456",
      "title": ".NET Backend Developer",
      "company": "CloudTech Solutions",
      "location": "Remote",
      "url": "https://simplify.jobs/p/cloudtech-solutions-dotnet-backend-developer",
      "salary": "$120,000 - $160,000",
      "description": "Join our growing cloud platform team...",
      "benefits": ["Health Insurance", "401k", "Remote Work"],
      "applicationLink": "https://simplify.jobs/apply/cloudtech-456"
    }
  ],
  "duration": 67.8,
  "errors": []
}
```
