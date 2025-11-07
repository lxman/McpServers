# market_intelligence

Generate comprehensive market analysis reports including salary trends, technology demand, and hiring patterns.

## Parameters

- **jobsJson** (string): JSON array of EnhancedJobListing objects to analyze
  - Example: `[{"title": "Senior .NET Developer", "company": "TechCorp", "salary": "$150,000 - $180,000", "location": "Remote", "description": "Looking for .NET Core experience..."}]`
- **requestJson** (string, optional): JSON MarketAnalysisRequest object with specific analysis preferences
  - Example: `{"includeForecasts": true, "includeTechDemand": true, "includeCompetitiveness": true, "targetLocations": ["San Francisco, CA", "Remote"]}`

## Returns

JSON string containing:
- **success** (boolean): Whether report generation completed successfully
- **reportDate** (string): Date when report was generated
- **periodAnalyzed** (object): Start and end dates analyzed
- **salaryAnalysis** (object): Salary trend data
  - **average** (number): Average salary
  - **median** (number): Median salary
  - **byExperience** (object): Salary by experience level
  - **byLocation** (object): Salary by location
  - **trend** (string): 'increasing', 'stable', 'decreasing'
- **techDemand** (object): Technology and skill demand
  - **topSkills** (array): Most demanded technologies
  - **emergingSkills** (array): Trending technologies
  - **declining** (array): Declining technologies
- **hiringPatterns** (object): Hiring trend analysis
  - **topHiringCompanies** (array): Companies posting most jobs
  - **byIndustry** (object): Jobs by industry
  - **growthRate** (number): Month-over-month growth percentage
- **competitiveness** (object): Market competitiveness metrics
  - **applicantsPerJob** (number): Estimated ratio
  - **difficulty** (string): 'easy', 'moderate', 'competitive', 'very_competitive'
- **forecast** (object, optional): 30-day forecast predictions
- **duration** (number): Time taken to generate report in seconds
- **errors** (array, optional): List of any errors encountered

## Example

```json
{
  "success": true,
  "reportDate": "2025-11-06T12:00:00Z",
  "periodAnalyzed": {
    "startDate": "2025-10-06",
    "endDate": "2025-11-06"
  },
  "salaryAnalysis": {
    "average": 175000,
    "median": 165000,
    "byExperience": {
      "entry": 95000,
      "mid": 140000,
      "senior": 195000,
      "principal": 240000
    },
    "byLocation": {
      "Remote": 170000,
      "San Francisco, CA": 210000,
      "New York, NY": 185000,
      "Austin, TX": 155000
    },
    "trend": "increasing"
  },
  "techDemand": {
    "topSkills": [
      {
        "skill": ".NET Core",
        "occurrences": 487,
        "growth": 15.2
      },
      {
        "skill": "Azure",
        "occurrences": 312,
        "growth": 22.1
      }
    ],
    "emergingSkills": ["Rust", "WebAssembly", "HTMX"],
    "declining": ["Classic ASP.NET", "Silverlight"]
  },
  "hiringPatterns": {
    "topHiringCompanies": [
      {"company": "Microsoft", "jobCount": 45},
      {"company": "Google", "jobCount": 38}
    ],
    "byIndustry": {
      "Technology": 892,
      "Finance": 234,
      "Healthcare": 156
    },
    "growthRate": 8.5
  },
  "competitiveness": {
    "applicantsPerJob": 12.3,
    "difficulty": "competitive"
  },
  "forecast": {
    "expectedGrowth": 5.2,
    "expectedSalaryGrowth": 2.1,
    "emergingSkillsForecasted": ["AI Integration", "Cloud Native"]
  },
  "duration": 28.6,
  "errors": []
}
```
