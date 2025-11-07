# Register Password Pattern

Registers a password for files matching a specific pattern. Useful for handling multiple documents with the same password.

## Parameters

- **pattern** (string, required): File path pattern (supports wildcards like * and ?)
- **password** (string, required): Password to decrypt matching documents

## Returns

Returns a JSON object with registration result:
```json
{
  "success": true,
  "pattern": "C:\\Documents\\Reports\\*.pdf",
  "message": "Password pattern registered successfully"
}
```

## Example

```javascript
// Register password for all PDFs in a directory
register_password_pattern({
  pattern: "C:\\Documents\\Confidential\\*.pdf",
  password: "secret123"
})

// Register password for specific file pattern
register_password_pattern({
  pattern: "C:\\Reports\\2024_Q*_report.pdf",
  password: "quarter2024"
})
```
