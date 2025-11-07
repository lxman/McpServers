# get_email_alert_summary

Get a summary of job alerts received from email sources including LinkedIn and Glassdoor.

## Parameters

- **daysBack** (int, optional): Number of days to look back for email alerts. Default: 7

## Returns

JSON string containing:
- **success** (boolean): Whether the operation completed successfully
- **summary** (object): Summary statistics of email alerts
  - **totalAlerts** (number): Total number of alerts found
  - **unreadCount** (number): Number of unread alerts
  - **bySource** (object): Breakdown by source (LinkedIn, Glassdoor)
  - **jobsCount** (number): Total number of unique jobs in alerts
- **alerts** (array, optional): Summary of each alert with key information
- **duration** (number): Time taken to retrieve summary in seconds
- **errors** (array, optional): List of any errors encountered

## Example

```json
{
  "success": true,
  "summary": {
    "totalAlerts": 45,
    "unreadCount": 12,
    "bySource": {
      "LinkedIn": 28,
      "Glassdoor": 17
    },
    "jobsCount": 38
  },
  "alerts": [
    {
      "id": "alert_12345",
      "source": "LinkedIn",
      "subject": "New .NET Developer opportunities",
      "jobCount": 5,
      "receivedDate": "2025-11-05T10:30:00Z",
      "unread": true
    }
  ],
  "duration": 2.3,
  "errors": []
}
```
