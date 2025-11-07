# Get Connection Strings

Get connection strings for a web app.

## Parameters
- **webAppName** (string): Web app name
- **resourceGroupName** (string): Resource group name
- **subscriptionId** (string, optional): Azure subscription ID

## Returns
JSON object with connection strings.

## Example Response
```json
{
  "success": true,
  "connectionStrings": [
    {
      "name": "DefaultConnection",
      "connectionString": "Server=...",
      "type": "SQLAzure"
    }
  ]
}
```
