# categorize_applications

Categorize jobs based on user preferences and intelligent classification rules.

## Parameters

- **jobsJson** (string): JSON array of EnhancedJobListing objects to categorize
  - Example: `[{"title": "Senior .NET Developer", "company": "TechCorp", "description": "Looking for backend developer with C# experience..."}]`
- **preferencesJson** (string, optional): JSON ApplicationPreferences object with custom categorization rules
  - Example: `{"preferredRoles": ["Backend Engineer", "Full Stack Developer"], "preferredTechnologies": [".NET Core", "Azure"], "minSalary": 150000}`

## Returns

JSON string containing:
- **success** (boolean): Whether categorization completed successfully
- **jobsCategorized** (number): Number of jobs with categories assigned
- **categorization** (object): Summary of jobs by category
- **uncategorized** (number): Number of jobs without clear category
- **categoryDistribution** (object): Breakdown of jobs across categories
- **topCategories** (array): Most common job categories
- **duration** (number): Time taken in seconds
- **errors** (array, optional): List of any errors encountered

## Example

```json
{
  "success": true,
  "jobsCategorized": 156,
  "categorization": {
    "Cloud Architect": 28,
    "Backend Engineer": 45,
    "Full Stack Developer": 32,
    "DevOps Engineer": 24,
    "Principal Engineer": 27
  },
  "uncategorized": 12,
  "categoryDistribution": {
    "Cloud Architect": {
      "count": 28,
      "avgScore": 85.3,
      "avgSalary": "$210,000"
    },
    "Backend Engineer": {
      "count": 45,
      "avgScore": 78.9,
      "avgSalary": "$165,000"
    }
  },
  "topCategories": [
    {"category": "Backend Engineer", "count": 45},
    {"category": "Principal Engineer", "count": 27},
    {"category": "Full Stack Developer", "count": 32}
  ],
  "duration": 12.4,
  "errors": []
}
```
