# Create Container

Create a new blob container in a storage account.

## Parameters
- **accountName** (string): Storage account name
- **containerName** (string): Container name to create
- **publicAccess** (string, optional): Public access level (default: "None")

## Returns
JSON object with success status and container information.

## Example Response
```json
{
  "success": true,
  "message": "Container created successfully",
  "containerName": "my-container",
  "accountName": "mystorageaccount"
}
```
