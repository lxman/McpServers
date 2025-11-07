# Put CloudWatch Metric Alarm

Create or update a CloudWatch metric alarm.

## Parameters

- **alarmName**: Name of the alarm
- **alarmDescription**: Description of the alarm
- **metricName**: Metric to monitor
- **namespace**: Metric namespace
- **statistic**: Statistic to apply
- **period**: Period in seconds
- **evaluationPeriods**: Number of periods to evaluate
- **threshold**: Threshold value
- **comparisonOperator**: Comparison operator (GreaterThanThreshold, etc.)

## Returns

Confirmation of alarm creation/update.

## Example Usage

```json
{
  "alarmName": "HighErrorRate",
  "metricName": "Errors",
  "namespace": "AWS/Lambda",
  "statistic": "Sum",
  "period": 300,
  "evaluationPeriods": 2,
  "threshold": 10,
  "comparisonOperator": "GreaterThanThreshold"
}
```