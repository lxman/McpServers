# get_site_config

Get configuration settings for a specific job site.

## Parameters

- **site** (string): The job site to get config for. Supported values: 'Dice', 'BuiltIn', 'AngelList', 'StackOverflow', 'HubSpot', 'SimplifyJobs'

## Returns

JSON string containing:
- **success** (boolean): Whether the configuration was retrieved successfully
- **site** (string): The requested job site
- **baseUrl** (string): Base URL for the site
- **isActive** (boolean): Whether scraping is enabled for this site
- **scrapingSettings** (object): Scraping configuration
  - **enabled** (boolean): Whether scraping is enabled
  - **updateFrequency** (string): How often to scrape (e.g., 'daily', 'hourly')
  - **timeout** (number): Scraping timeout in seconds
  - **maxPages** (number): Maximum pages to scrape
  - **userAgent** (string, optional): Custom user agent
- **authentication** (object, optional): Authentication settings if required
  - **required** (boolean): Whether authentication is needed
  - **type** (string): Type of authentication
- **lastUpdated** (string): When configuration was last updated
- **errors** (array, optional): List of any errors encountered

## Example

```json
{
  "success": true,
  "site": "Dice",
  "baseUrl": "https://www.dice.com",
  "isActive": true,
  "scrapingSettings": {
    "enabled": true,
    "updateFrequency": "daily",
    "timeout": 30,
    "maxPages": 5,
    "userAgent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"
  },
  "authentication": {
    "required": false,
    "type": "none"
  },
  "lastUpdated": "2025-11-01T08:00:00Z",
  "errors": []
}
```
