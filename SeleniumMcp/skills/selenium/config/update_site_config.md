# update_site_config

Update configuration settings for a specific job site.

## Parameters

- **configJson** (string): JSON SiteConfiguration object with updated settings
  - Example: `{"siteName": "Dice", "enabled": true, "maxPages": 10, "timeout": 45, "baseUrl": "https://www.dice.com"}`

## Returns

JSON string containing:
- **success** (boolean): Whether the configuration update completed successfully
- **site** (string): The updated site
- **previousSettings** (object): Settings before update
- **newSettings** (object): Settings after update
- **changedFields** (array): List of fields that were changed
- **validationErrors** (array, optional): Any validation errors if validateOnly was true
- **errors** (array, optional): List of any errors encountered

## Example

```json
{
  "success": true,
  "site": "Dice",
  "previousSettings": {
    "updateFrequency": "daily",
    "timeout": 30,
    "maxPages": 5
  },
  "newSettings": {
    "updateFrequency": "hourly",
    "timeout": 45,
    "maxPages": 10
  },
  "changedFields": [
    "updateFrequency",
    "timeout",
    "maxPages"
  ],
  "validationErrors": [],
  "errors": []
}
```
