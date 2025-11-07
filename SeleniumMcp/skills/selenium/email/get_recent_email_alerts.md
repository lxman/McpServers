# get_recent_email_alerts

Get the most recent email alerts from job sources, with options for filtering and pagination.

## Parameters

No parameters required for this operation.

## Returns

JSON string containing:
- **success** (boolean): Whether the operation completed successfully
- **alerts** (array): Array of recent email alerts
- **alertCount** (number): Number of alerts retrieved
- **unreadCount** (number): Number of unread alerts in results
- **totalJobsInAlerts** (number): Total jobs mentioned in all alerts
- **oldestAlertDate** (string): Date of the oldest alert retrieved in ISO 8601 format
- **newestAlertDate** (string): Date of the newest alert retrieved in ISO 8601 format
- **errors** (array, optional): List of any errors encountered

## Example

```json
{
  "success": true,
  "alerts": [
    {
      "id": "alert_98765",
      "source": "LinkedIn",
      "subject": ".NET Developer roles in your area",
      "receivedDate": "2025-11-06T08:45:00Z",
      "unread": true,
      "jobCount": 8,
      "jobs": [
        {
          "title": "Senior .NET Engineer",
          "company": "CloudInnovate",
          "location": "San Francisco, CA",
          "url": "https://linkedin.com/jobs/view/9876543",
          "salary": "$180,000 - $220,000"
        }
      ]
    }
  ],
  "alertCount": 15,
  "unreadCount": 5,
  "totalJobsInAlerts": 67,
  "oldestAlertDate": "2025-11-05T08:45:00Z",
  "newestAlertDate": "2025-11-06T08:45:00Z",
  "errors": []
}
```
