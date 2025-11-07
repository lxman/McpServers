# Clear Passwords

Clears all registered passwords and password patterns from the system. Use with caution as this requires re-registering passwords for encrypted documents.

## Parameters

None

## Returns

Returns a JSON object with clearing result:
```json
{
  "success": true,
  "clearedPasswords": 18,
  "clearedPatterns": 7,
  "message": "All passwords cleared successfully"
}
```

## Example

```javascript
clear_passwords()
```
