# Register Password

Registers a password for a specific encrypted document. The password is stored securely and used automatically when accessing the document.

## Parameters

- **filePath** (string, required): Full path to the encrypted document file
- **password** (string, required): Password to decrypt the document

## Returns

Returns a JSON object with registration result:
```json
{
  "success": true,
  "filePath": "/path/to/encrypted.pdf",
  "message": "Password registered successfully"
}
```

## Example

```javascript
register_password({
  filePath: "C:\\Documents\\confidential.pdf",
  password: "secret123"
})
```
