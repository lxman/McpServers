# Get Current Time

## Description
Returns the current date and time in multiple formats including ISO 8601, Unix timestamps, timezone information, and human-readable formats.

## Parameters
None

## Returns
JSON object containing:
- `localTime` (string): Current time in ISO 8601 format (local timezone)
- `utcTime` (string): Current time in ISO 8601 format (UTC)
- `formattedLocal` (string): Human-readable local time format
- `unixMilliseconds` (number): Current Unix timestamp in milliseconds
- `unixSeconds` (number): Current Unix timestamp in seconds
- `timezone` (string): System timezone identifier
- `utcOffset` (string): UTC offset in format +HH:MM or -HH:MM

## Example JSON

### Request
```json
{}
```

### Response
```json
{
  "localTime": "2024-11-06T14:35:42.5638920-05:00",
  "utcTime": "2024-11-06T19:35:42.5638920Z",
  "formattedLocal": "Wednesday, November 6, 2024 2:35:42 PM",
  "unixMilliseconds": 1730895342563,
  "unixSeconds": 1730895342,
  "timezone": "Eastern Standard Time",
  "utcOffset": "-05:00"
}
```
