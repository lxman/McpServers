# Disable CloudWatch Metric Alarms

Disable actions for one or more CloudWatch alarms.

## Parameters

- **alarmNames**: List of alarm names to disable

## Returns

Confirmation of alarm disabling.

## Example Usage

```json
{
  "alarmNames": ["HighErrorRate", "HighLatency"]
}
```