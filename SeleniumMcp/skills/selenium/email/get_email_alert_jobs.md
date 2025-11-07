# get_email_alert_jobs

Retrieve jobs from email alerts with comprehensive filtering by source and other criteria.

## Parameters

- **daysBack** (int, optional): Number of days to look back for email alerts. Default: 7
- **source** (string, optional): Filter by email source. Supported values: 'LinkedIn', 'Glassdoor'

## Returns

JSON string containing:
- **success** (boolean): Whether the operation completed successfully
- **jobs** (array): Array of jobs extracted from email alerts
- **jobCount** (number): Total number of jobs retrieved
- **sourceBreakdown** (object): Count of jobs by source
- **imported** (number, optional): Number of jobs imported to storage if enabled
- **uniqueCompanies** (number): Number of unique companies in retrieved jobs
- **duration** (number): Time taken to retrieve jobs in seconds
- **errors** (array, optional): List of any errors encountered

## Example

```json
{
  "success": true,
  "jobs": [
    {
      "id": "email_alert_789",
      "title": "C# Backend Developer",
      "company": "DataFlow Systems",
      "location": "Austin, TX",
      "url": "https://linkedin.com/jobs/view/1234567",
      "source": "LinkedIn",
      "description": "Looking for experienced C# developer...",
      "receivedDate": "2025-11-05T09:15:00Z",
      "applicationType": "remote",
      "salary": "$130,000 - $160,000"
    }
  ],
  "jobCount": 23,
  "sourceBreakdown": {
    "LinkedIn": 18,
    "Glassdoor": 5
  },
  "imported": 23,
  "uniqueCompanies": 19,
  "duration": 4.5,
  "errors": []
}
```
