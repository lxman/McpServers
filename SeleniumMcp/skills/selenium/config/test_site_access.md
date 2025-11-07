# test_site_access

Test if a job site is accessible and responsive for scraping operations.

## Parameters

- **site** (string): The job site to test. Supported values: 'Dice', 'BuiltIn', 'AngelList', 'StackOverflow', 'HubSpot', 'SimplifyJobs'

## Returns

JSON string containing:
- **success** (boolean): Whether the test completed successfully
- **site** (string): The tested site
- **accessible** (boolean): Whether the site is accessible
- **responseStatus** (number): HTTP response status code
- **responseTime** (number): Response time in milliseconds
- **testType** (string): Type of test performed
- **authentication** (object, optional): Authentication test results
  - **required** (boolean): Whether authentication is required
  - **successful** (boolean): Whether authentication succeeded
  - **message** (string, optional): Authentication status message
- **warnings** (array, optional): Any warnings encountered
- **errors** (array, optional): Any errors encountered
- **recommendations** (array, optional): Recommendations for improving scraping

## Example

```json
{
  "success": true,
  "site": "Dice",
  "accessible": true,
  "responseStatus": 200,
  "responseTime": 1247,
  "testType": "full",
  "authentication": {
    "required": false,
    "successful": true,
    "message": "No authentication required"
  },
  "warnings": [],
  "errors": [],
  "recommendations": [
    "Site is responding normally",
    "No rate limiting detected",
    "Scraping should work without issues"
  ]
}
```
