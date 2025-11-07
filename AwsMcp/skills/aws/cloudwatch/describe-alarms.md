# Describe CloudWatch Alarms

List and describe CloudWatch metric alarms.

## Parameters

- **alarmNames**: List of specific alarm names (optional)
- **alarmNamePrefix**: Filter by alarm name prefix (optional)
- **stateValue**: Filter by state (OK, ALARM, INSUFFICIENT_DATA)

## Returns

List of alarms with their configurations and states.

## Example Usage

```json
{
  "stateValue": "ALARM"
}
```