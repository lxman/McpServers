# simplify_jobs_enhanced

Enhanced SimplifyJobs processing with intelligent scoring and comprehensive job analysis.

## Parameters

- **searchTerm** (string): Job title or keyword to search for
- **location** (string): Geographic location for the job search
- **maxResults** (int, optional): Maximum number of results to return. Default: 50

## Returns

JSON string containing:
- **success** (boolean): Whether the operation completed successfully
- **jobsFound** (number): Total SimplifyJobs listings found
- **jobsProcessed** (number): Number of jobs processed with scoring
- **jobsSaved** (number): Number of jobs saved to storage
- **averageScore** (number): Average match score for processed jobs (0-100)
- **topMatches** (array): Top 5 best matching SimplifyJobs listings
- **companyInsights** (object): Analysis of companies in results
  - **uniqueCompanies** (number): Number of unique companies
  - **topCompanies** (array): Most frequently hiring companies
  - **avgCompanyRating** (number): Average company rating
- **duration** (number): Time taken in seconds
- **errors** (array, optional): List of any errors encountered

## Example

```json
{
  "success": true,
  "jobsFound": 47,
  "jobsProcessed": 47,
  "jobsSaved": 47,
  "averageScore": 81.3,
  "topMatches": [
    {
      "id": "simplify_enhanced_001",
      "title": "Senior .NET Backend Engineer",
      "company": "CloudScale Inc",
      "location": "Remote",
      "salary": "$160,000 - $200,000",
      "score": 96,
      "companyRating": 4.7,
      "benefits": ["Health Insurance", "401k", "Stock Options", "Remote"],
      "url": "https://simplify.jobs/p/cloudscale-dotnet-engineer"
    }
  ],
  "companyInsights": {
    "uniqueCompanies": 38,
    "topCompanies": [
      {"company": "Tech Innovators", "count": 4},
      {"company": "Cloud Systems", "count": 3}
    ],
    "avgCompanyRating": 4.4
  },
  "duration": 67.8,
  "errors": []
}
```
