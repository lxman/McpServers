# scrape_multiple_sites

Scrape jobs from multiple job sites simultaneously to gather comprehensive job listings across all supported platforms.

## Parameters

- **sitesJson** (string): JSON array of site names to scrape. Supported values: 'Dice', 'BuiltIn', 'AngelList', 'StackOverflow', 'HubSpot', 'SimplifyJobs'
  - Example: `["Dice", "BuiltIn", "SimplifyJobs"]`
- **searchTerm** (string): Job title or keyword to search for
- **location** (string): Geographic location for the job search
- **userId** (string, optional): User identifier for tracking. Default: "default_user"

## Returns

JSON string containing:
- **success** (boolean): Whether the scraping operation completed successfully
- **scrapedJobs** (array): Array of job objects containing title, company, location, url, source, salary, description
- **jobCount** (number): Total number of jobs scraped across all sites
- **sites** (object): Breakdown of job counts per site
- **duration** (number): Time taken to complete scraping in seconds
- **errors** (array, optional): List of any errors encountered during scraping

## Example

```json
{
  "success": true,
  "scrapedJobs": [
    {
      "title": "Senior .NET Developer",
      "company": "TechCorp",
      "location": "New York, NY",
      "url": "https://example.com/job/123",
      "source": "Dice",
      "salary": "$150,000 - $180,000",
      "description": "We are looking for an experienced .NET developer..."
    }
  ],
  "jobCount": 45,
  "sites": {
    "Dice": 10,
    "BuiltIn": 8,
    "AngelList": 12,
    "StackOverflow": 9,
    "HubSpot": 6,
    "SimplifyJobs": 0
  },
  "duration": 127.5,
  "errors": []
}
```
