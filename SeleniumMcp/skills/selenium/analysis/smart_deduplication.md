# smart_deduplication

Remove duplicate jobs intelligently across multiple sources with configurable matching strategies.

## Parameters

- **jobsJson** (string): JSON array of EnhancedJobListing objects to deduplicate
  - Example: `[{"title": "Senior .NET Developer", "company": "TechCorp", "url": "https://example.com/job/123"}, {"title": "Senior .NET Developer", "company": "TechCorp", "url": "https://example.com/job/456"}]`

## Returns

JSON string containing:
- **success** (boolean): Whether the deduplication completed successfully
- **jobsAnalyzed** (number): Total jobs analyzed for duplicates
- **duplicatesFound** (number): Number of duplicate groups identified
- **jobsRemoved** (number): Number of duplicate job records removed
- **jobsMerged** (number): Number of jobs merged with enhanced data
- **duplicateGroups** (array): Details of duplicate groups found
- **storageReclaimed** (number, optional): Approximate storage space freed in bytes
- **duration** (number): Time taken in seconds
- **dryRunResults** (boolean, optional): Whether this was a dry run with no actual changes
- **errors** (array, optional): List of any errors encountered

## Example

```json
{
  "success": true,
  "jobsAnalyzed": 287,
  "duplicatesFound": 28,
  "jobsRemoved": 34,
  "jobsMerged": 28,
  "duplicateGroups": [
    {
      "groupId": "dup_group_001",
      "duplicateCount": 3,
      "jobs": [
        {
          "id": "507f1f77bcf86cd799439011",
          "title": "Senior .NET Developer",
          "source": "Dice",
          "url": "https://dice.com/jobs/123"
        },
        {
          "id": "507f1f77bcf86cd799439012",
          "title": "Senior .NET Developer",
          "source": "BuiltIn",
          "url": "https://builtin.com/jobs/456"
        }
      ],
      "mergedInto": "507f1f77bcf86cd799439011",
      "confidence": 0.98
    }
  ],
  "storageReclaimed": 2457600,
  "duration": 34.5,
  "dryRunResults": false,
  "errors": []
}
```
