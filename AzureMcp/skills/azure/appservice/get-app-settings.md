# Get App Settings

Get application settings for a web app.

## Parameters
- **webAppName** (string): Web app name
- **resourceGroupName** (string): Resource group name
- **subscriptionId** (string, optional): Azure subscription ID

## Returns
JSON object with app settings.

## Example Response
```json
{
  "success": true,
  "settings": {
    "WEBSITE_NODE_DEFAULT_VERSION": "14.17.0",
    "CUSTOM_SETTING": "value"
  }
}
```
