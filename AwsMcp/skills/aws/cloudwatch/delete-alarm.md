# Delete CloudWatch Metric Alarms

Delete one or more CloudWatch metric alarms.

## Parameters

- **alarmNames**: List of alarm names to delete

## Returns

Confirmation of alarm deletion.

## Example Usage

```json
{
  "alarmNames": ["HighErrorRate", "HighLatency"]
}
```

## Warning

This action is irreversible.