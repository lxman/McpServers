# Enable CloudWatch Metric Alarms

Enable actions for one or more CloudWatch alarms.

## Parameters

- **alarmNames**: List of alarm names to enable

## Returns

Confirmation of alarm enabling.

## Example Usage

```json
{
  "alarmNames": ["HighErrorRate", "HighLatency"]
}
```