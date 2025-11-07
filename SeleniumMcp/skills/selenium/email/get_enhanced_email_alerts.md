# get_enhanced_email_alerts

Get email alerts with enhanced details including match scoring, company information, and intelligent recommendations.

## Parameters

- **daysBack** (int, optional): Number of days to look back for email alerts. Default: 7

## Returns

JSON string containing:
- **success** (boolean): Whether the operation completed successfully
- **alerts** (array): Array of enhanced email alerts with scoring and details
- **alertCount** (number): Number of alerts retrieved
- **jobsAnalyzed** (number): Total number of jobs analyzed
- **avgMatchScore** (number): Average match score across all jobs
- **topMatches** (number): Count of jobs scoring above 80
- **duration** (number): Time taken to retrieve and enhance alerts in seconds
- **errors** (array, optional): List of any errors encountered

## Example

```json
{
  "success": true,
  "alerts": [
    {
      "id": "alert_enhanced_123",
      "source": "LinkedIn",
      "subject": "C# opportunities matching your profile",
      "receivedDate": "2025-11-06T10:00:00Z",
      "jobs": [
        {
          "title": "Principal .NET Architect",
          "company": "TechCorp",
          "location": "Remote",
          "url": "https://linkedin.com/jobs/view/5432109",
          "salary": "$200,000 - $250,000",
          "matchScore": 95,
          "scoreBreakdown": {
            "titleMatch": 98,
            "experienceMatch": 92,
            "salaryMatch": 91,
            "locationMatch": 100
          },
          "companyInfo": {
            "name": "TechCorp",
            "industry": "Software Development",
            "size": "1000-5000",
            "founded": 2010,
            "reviews": 4.5
          },
          "recommendations": [
            "Strong match for your .NET expertise",
            "Salary aligns with your expectations",
            "Company has excellent reviews and stability"
          ]
        }
      ]
    }
  ],
  "alertCount": 12,
  "jobsAnalyzed": 47,
  "avgMatchScore": 78.5,
  "topMatches": 18,
  "duration": 8.7,
  "errors": []
}
```
