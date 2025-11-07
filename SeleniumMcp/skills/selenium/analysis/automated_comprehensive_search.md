# automated_comprehensive_search

Run an automated search across multiple job search terms and locations simultaneously across all supported job sites.

## Parameters

- **searchTermsJson** (string): JSON array of job titles or keywords to search for
  - Example: `[".NET Developer", "C# Engineer", "Software Architect"]`
- **locationsJson** (string): JSON array of geographic locations to search
  - Example: `["San Francisco, CA", "Remote", "New York, NY"]`
- **targetJobsPerSearch** (int, optional): Target number of jobs to retrieve per search combination. Default: 20

## Returns

JSON string containing:
- **success** (boolean): Whether the automated search completed successfully
- **totalJobsFound** (number): Total number of unique jobs discovered
- **jobsBySite** (object): Breakdown of jobs by each site
- **jobsByLocation** (object): Breakdown of jobs by location
- **jobsBySalaryRange** (object): Distribution of salary ranges
- **duplicatesRemoved** (number): Number of duplicate jobs removed
- **savedToStorage** (number): Number of jobs saved to MongoDB
- **reportGenerated** (boolean): Whether market report was generated
- **duration** (number): Total time in seconds
- **searchCombinations** (number): Number of search combinations executed
- **errors** (array, optional): List of any errors encountered

## Example

```json
{
  "success": true,
  "totalJobsFound": 156,
  "jobsBySite": {
    "Dice": 34,
    "BuiltIn": 28,
    "AngelList": 31,
    "StackOverflow": 22,
    "HubSpot": 19,
    "SimplifyJobs": 22
  },
  "jobsByLocation": {
    "Remote": 67,
    "San Francisco, CA": 45,
    "New York, NY": 38,
    "Austin, TX": 6
  },
  "jobsBySalaryRange": {
    "100k-130k": 23,
    "130k-160k": 58,
    "160k-200k": 42,
    "200k+": 33
  },
  "duplicatesRemoved": 12,
  "savedToStorage": 144,
  "reportGenerated": true,
  "duration": 287.5,
  "searchCombinations": 8,
  "errors": []
}
```
