# enhanced_job_analysis

Perform detailed job analysis with intelligent scoring based on developer profile and preferences.

## Parameters

- **jobsJson** (string): JSON array of EnhancedJobListing objects to analyze
  - Example: `[{"title": "Principal Engineer - .NET Platform", "company": "TechCorp", "salary": "$250,000 - $300,000", "location": "Remote", "description": "Lead .NET architecture..."}]`
- **profileJson** (string, optional): JSON scoring profile object for custom analysis
  - Example: `{"experienceYears": 50, "preferredRoles": ["Principal Engineer", "Architect"], "desiredSalary": 250000, "technologiesExperienced": [".NET Core", "Azure", "Microservices"], "remotePreference": "required"}`

## Returns

JSON string containing:
- **success** (boolean): Whether the analysis completed successfully
- **jobsAnalyzed** (number): Total number of jobs analyzed
- **analyzeProfile** (string): Profile used for analysis
- **scores** (array): Detailed scoring for each job
  - **jobId** (string): Job ID
  - **title** (string): Job title
  - **company** (string): Company name
  - **overallScore** (number): Overall match score (0-100)
  - **scoreBreakdown** (object): Detailed score components
    - **roleMatch** (number): How well role matches profile
    - **technicalMatch** (number): Technology skill match
    - **salaryMatch** (number): Salary vs. expectations
    - **locationMatch** (number): Location preference match
    - **companyMatch** (number): Company quality rating
  - **recommendation** (string): Whether to apply
  - **reasoning** (array): Reasons for the score
- **topMatches** (array): Top 5 jobs by score
- **statistics** (object): Scoring statistics
  - **averageScore** (number): Average score across all jobs
  - **highestScore** (number): Highest score
  - **lowestScore** (number): Lowest score
  - **medianScore** (number): Median score
- **marketComparison** (object, optional): How scores compare to market averages
- **duration** (number): Time taken in seconds
- **errors** (array, optional): List of any errors encountered

## Example

```json
{
  "success": true,
  "jobsAnalyzed": 45,
  "analyzeProfile": "50year_dotnet",
  "scores": [
    {
      "jobId": "507f1f77bcf86cd799439011",
      "title": "Principal Engineer - .NET Platform",
      "company": "TechCorp",
      "overallScore": 97,
      "scoreBreakdown": {
        "roleMatch": 99,
        "technicalMatch": 96,
        "salaryMatch": 98,
        "locationMatch": 95,
        "companyMatch": 96
      },
      "recommendation": "Highly Recommended - Apply Immediately",
      "reasoning": [
        "Perfect role match for principal level",
        "Strong technology alignment",
        "Excellent salary for experience level",
        "Remote opportunity matches preference"
      ]
    }
  ],
  "topMatches": [
    {
      "jobId": "507f1f77bcf86cd799439011",
      "title": "Principal Engineer - .NET Platform",
      "company": "TechCorp",
      "score": 97
    }
  ],
  "statistics": {
    "averageScore": 76.8,
    "highestScore": 97,
    "lowestScore": 28,
    "medianScore": 78
  },
  "marketComparison": {
    "averageVsMarket": 3.2,
    "percentageAboveAverage": 68
  },
  "duration": 23.4,
  "errors": []
}
```
