# Get Password Patterns

Retrieves all registered password patterns and their associated file patterns.

## Parameters

None

## Returns

Returns a JSON object with password patterns:
```json
{
  "success": true,
  "patterns": [
    {
      "pattern": "C:\\Documents\\Confidential\\*.pdf",
      "registeredAt": "2025-11-06T10:30:00Z",
      "matchingFiles": 15
    },
    {
      "pattern": "C:\\Reports\\2024_Q*_report.pdf",
      "registeredAt": "2025-11-06T11:00:00Z",
      "matchingFiles": 4
    }
  ],
  "totalCount": 2
}
```

## Example

```javascript
get_password_patterns()
```
