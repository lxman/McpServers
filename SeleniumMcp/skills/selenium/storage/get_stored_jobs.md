# get_stored_jobs

Retrieve stored jobs from MongoDB with comprehensive filtering and sorting options.

## Parameters

- **userId** (string): User identifier to retrieve jobs for
- **searchTerm** (string, optional): Search keyword to filter jobs by title or description
- **location** (string, optional): Filter jobs by location
- **minSalary** (int, optional): Minimum salary threshold for filtering
- **remoteOnly** (bool, optional): Filter for remote jobs only
- **skip** (int, optional): Number of results to skip for pagination. Default: 0
- **limit** (int, optional): Maximum number of results to return. Default: 50

## Returns

JSON string containing:
- **success** (boolean): Whether the retrieval operation completed successfully
- **jobs** (array): Array of job objects from storage
- **totalCount** (number): Total number of jobs matching the query
- **returnedCount** (number): Number of jobs returned in this request
- **hasMore** (boolean): Whether there are more results available
- **errors** (array, optional): List of any errors encountered

## Example

```json
{
  "success": true,
  "jobs": [
    {
      "id": "507f1f77bcf86cd799439011",
      "title": "Senior .NET Developer",
      "company": "TechCorp",
      "location": "New York, NY",
      "url": "https://example.com/job/123",
      "source": "Dice",
      "salary": "$150,000 - $180,000",
      "description": "We are looking for...",
      "postedDate": "2025-11-05",
      "score": 92,
      "screenshotUrl": "mongodb://screenshots/507f1f77bcf86cd799439011.png"
    }
  ],
  "totalCount": 287,
  "returnedCount": 1,
  "hasMore": true,
  "errors": []
}
```
