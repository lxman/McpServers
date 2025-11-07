# Get Password Stats

Retrieves statistics about password registrations and usage across the system.

## Parameters

None

## Returns

Returns a JSON object with password statistics:
```json
{
  "success": true,
  "statistics": {
    "totalRegistered": 25,
    "directPasswords": 18,
    "patternPasswords": 7,
    "successfulAccesses": 142,
    "failedAccesses": 3,
    "lastAccessed": "2025-11-06T14:30:00Z",
    "encryptedDocuments": 25,
    "unencryptedDocuments": 75
  }
}
```

## Example

```javascript
get_password_stats()
```
