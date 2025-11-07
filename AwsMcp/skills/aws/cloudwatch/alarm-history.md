# Get CloudWatch Alarm History

Retrieve history of CloudWatch alarm state changes.

## Parameters

- **alarmName**: Specific alarm name (optional)
- **alarmTypes**: List of alarm types to filter (optional)
- **historyItemType**: Filter by history item type (optional)
- **startDate**: Start date for history
- **endDate**: End date for history
- **maxRecords**: Maximum records to return

## Returns

Alarm history records.

## Example Usage

```json
{
  "alarmName": "HighErrorRate",
  "startDate": "2024-01-01T00:00:00Z",
  "endDate": "2024-01-02T00:00:00Z",
  "maxRecords": 50
}
```