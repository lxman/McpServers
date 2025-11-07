# update_application_status

Update the status of a tracked job application with notes and timestamps.

## Parameters

- **applicationId** (string): The unique identifier of the application to update
- **status** (ApplicationStatus enum): New application status. Valid values: Applied, Screening, Interview, Offer, Rejected, Withdrawn
- **notes** (string, optional): Additional notes about the status change

## Returns

JSON string containing:
- **success** (boolean): Whether the status update completed successfully
- **trackingId** (string): The tracking ID that was updated
- **jobTitle** (string): Title of the tracked job
- **previousStatus** (string): Previous application status
- **newStatus** (string): Updated status
- **updatedAt** (string): Timestamp of update
- **statusTimeline** (array): Complete timeline of status changes
- **nextAction** (string, optional): Recommended next action
- **errors** (array, optional): List of any errors encountered

## Example

```json
{
  "success": true,
  "trackingId": "track_507f1f77bcf86cd799439011",
  "jobTitle": "Senior .NET Developer",
  "previousStatus": "applied",
  "newStatus": "screening",
  "updatedAt": "2025-11-06T15:45:00Z",
  "statusTimeline": [
    {
      "status": "applied",
      "date": "2025-11-06T10:30:00Z",
      "notes": "Initial application submitted"
    },
    {
      "status": "screening",
      "date": "2025-11-06T15:45:00Z",
      "notes": "Recruiter phone screening scheduled"
    }
  ],
  "nextAction": "Prepare for phone screening call on 2025-11-08",
  "errors": []
}
```
