# Register Bulk Passwords

Registers passwords for multiple documents in a single operation. Efficient for setting up access to many encrypted files.

## Parameters

- **filePasswords** (array, required): Array of objects containing file paths and passwords:
  - `filePath` (string): Full path to the document
  - `password` (string): Password for the document

## Returns

Returns a JSON object with bulk registration results:
```json
{
  "success": true,
  "registeredCount": 10,
  "failedCount": 0,
  "results": [
    {
      "filePath": "/path/to/document1.pdf",
      "success": true
    }
  ]
}
```

## Example

```javascript
register_bulk_passwords({
  filePasswords: [
    {
      filePath: "C:\\Documents\\report1.pdf",
      password: "password1"
    },
    {
      filePath: "C:\\Documents\\report2.pdf",
      password: "password2"
    },
    {
      filePath: "C:\\Documents\\report3.pdf",
      password: "password3"
    }
  ]
})
```
