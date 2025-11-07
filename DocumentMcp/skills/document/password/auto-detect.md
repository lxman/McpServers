# Auto Detect Password

Attempts to automatically detect the password for an encrypted document by trying common passwords or a provided list.

## Parameters

- **filePath** (string, required): Full path to the encrypted document file
- **commonPasswords** (array, optional): Array of passwords to try. If not provided, uses built-in common password list

## Returns

Returns a JSON object with detection result:
```json
{
  "success": true,
  "filePath": "/path/to/encrypted.pdf",
  "passwordFound": true,
  "password": "password123",
  "attempts": 15,
  "message": "Password detected successfully"
}
```

If password not found:
```json
{
  "success": false,
  "filePath": "/path/to/encrypted.pdf",
  "passwordFound": false,
  "attempts": 100,
  "message": "Password not found after 100 attempts"
}
```

## Example

```javascript
// Use default common passwords
auto_detect_password({
  filePath: "C:\\Documents\\encrypted.pdf"
})

// Provide custom password list
auto_detect_password({
  filePath: "C:\\Documents\\encrypted.pdf",
  commonPasswords: ["password123", "secret", "admin", "12345"]
})
```
