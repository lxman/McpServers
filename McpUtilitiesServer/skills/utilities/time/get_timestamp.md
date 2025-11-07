# Get Timestamp

## Description
Returns the current timestamp in multiple standardized formats including ISO 8601, RFC 1123, RFC 3339, Unix timestamps, and local/UTC datetime representations.

## Parameters
None

## Returns
JSON object containing:
- `iso8601` (string): Current time in ISO 8601 format
- `rfc1123` (string): Current time in RFC 1123 format (HTTP header compatible)
- `rfc3339` (string): Current time in RFC 3339 format (subset of ISO 8601)
- `unixMilliseconds` (number): Unix timestamp in milliseconds
- `unixSeconds` (number): Unix timestamp in seconds
- `localDateTime` (string): Local datetime representation
- `utcDateTime` (string): UTC datetime representation

## Example JSON

### Request
```json
{}
```

### Response
```json
{
  "iso8601": "2024-11-06T19:35:42.5638920Z",
  "rfc1123": "Wed, 06 Nov 2024 19:35:42 GMT",
  "rfc3339": "2024-11-06T19:35:42.5638920+00:00",
  "unixMilliseconds": 1730895342563,
  "unixSeconds": 1730895342,
  "localDateTime": "2024-11-06 14:35:42.5638920 (EST)",
  "utcDateTime": "2024-11-06 19:35:42.5638920 (UTC)"
}
```
