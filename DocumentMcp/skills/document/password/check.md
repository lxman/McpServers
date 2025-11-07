# Check Password

Checks if a password is registered for a specific document and verifies if it's valid.

## Parameters

- **filePath** (string, required): Full path to the document file

## Returns

Returns a JSON object with password status:
```json
{
  "success": true,
  "filePath": "/path/to/document.pdf",
  "hasPassword": true,
  "passwordValid": true,
  "encrypted": true,
  "source": "direct",
  "message": "Valid password registered"
}
```

Sources can be:
- `direct` - Password registered directly for this file
- `pattern` - Password from a matching pattern
- `none` - No password registered

## Example

```javascript
check_password({
  filePath: "C:\\Documents\\encrypted.pdf"
})
```
