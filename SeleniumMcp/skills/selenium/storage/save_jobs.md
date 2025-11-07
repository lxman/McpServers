# save_jobs

Save filtered jobs to MongoDB with intelligent deduplication and screenshot storage.

## Parameters

- **jobsJson** (string): JSON array of EnhancedJobListing objects to save
  - Example: `[{"title": "Senior .NET Developer", "company": "TechCorp", "location": "Remote", "url": "https://example.com/job/123", "source": "Dice", "salary": "$150,000 - $180,000", "description": "We are looking for..."}]`
- **userId** (string): User identifier for tracking and organization
- **sessionId** (string): Session identifier to group related job saves

## Returns

JSON string containing:
- **success** (boolean): Whether the save operation completed successfully
- **savedCount** (number): Number of jobs successfully saved
- **filteredOutCount** (number): Number of jobs filtered out
- **duplicatesSkipped** (number): Number of duplicate jobs skipped or merged
- **screenshotsSaved** (number): Number of screenshots captured and stored
- **jobIds** (array): Array of MongoDB IDs for saved jobs
- **errors** (array, optional): List of any errors encountered

## Example

```json
{
  "success": true,
  "savedCount": 18,
  "filteredOutCount": 5,
  "duplicatesSkipped": 2,
  "screenshotsSaved": 18,
  "jobIds": [
    "507f1f77bcf86cd799439011",
    "507f1f77bcf86cd799439012",
    "507f1f77bcf86cd799439013"
  ],
  "errors": []
}
```
