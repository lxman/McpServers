# track_application

Track a new job application with status, timestamps, and optional notes.

## Parameters

- **applicationJson** (string): JSON ApplicationRecord object with application details
  - Example: `{"jobId": "507f1f77bcf86cd799439011", "applicationDate": "2025-11-06T10:30:00Z", "status": "Applied", "notes": "Initial application submitted", "contactEmail": "recruiter@techcorp.com"}`
  - Status must be one of: Applied, Screening, Interview, Offer, Rejected, Withdrawn

## Returns

JSON string containing:
- **success** (boolean): Whether the application was tracked successfully
- **trackingId** (string): Unique identifier for this application tracking record
- **jobId** (string): Associated job ID
- **jobTitle** (string): Title of the job applied to
- **company** (string): Company name
- **applicationDate** (string): Date of application
- **status** (string): Current application status
- **nextFollowUp** (string, optional): Recommended follow-up date
- **lastUpdated** (string): Timestamp of last update
- **errors** (array, optional): List of any errors encountered

## Example

```json
{
  "success": true,
  "trackingId": "track_507f1f77bcf86cd799439011",
  "jobId": "507f1f77bcf86cd799439011",
  "jobTitle": "Senior .NET Developer",
  "company": "TechCorp",
  "applicationDate": "2025-11-06T10:30:00Z",
  "status": "applied",
  "nextFollowUp": "2025-11-13",
  "lastUpdated": "2025-11-06T10:30:00Z",
  "errors": []
}
```
