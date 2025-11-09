# scrape_site

Scrape jobs from a specific job site with optional Google Custom Search discovery for enhanced results.

**⚠️ Auto-Save**: All scraped jobs are automatically saved to a temporary collection for recovery. Use `consolidate_temporary_results` to save them permanently if needed.

## Parameters

- **site** (string): The job site to scrape. Supported values: 'Dice', 'BuiltIn', 'AngelList', 'StackOverflow', 'HubSpot', 'SimplifyJobs'
- **searchTerm** (string): Job title or keyword to search for
- **location** (string): Geographic location for the job search
- **userId** (string, optional): User identifier for tracking
- **useGoogleDiscovery** (boolean, optional): Use Google Custom Search to enhance job discovery. Default: false

## Returns

JSON string containing:
- **success** (boolean): Whether the scraping operation completed successfully
- **site** (string): The site that was scraped
- **scrapedJobs** (array): Array of job objects with full details
- **jobCount** (number): Total number of jobs found
- **googleDiscoveryCount** (number, optional): Number of jobs found through Google discovery if enabled
- **duration** (number): Time taken to complete scraping in seconds
- **errors** (array, optional): List of any errors encountered

## Example

```json
{
  "success": true,
  "site": "Dice",
  "scrapedJobs": [
    {
      "id": "dice_12345",
      "title": "Principal .NET Architect",
      "company": "Innovation Labs",
      "location": "San Francisco, CA",
      "url": "https://www.dice.com/jobs/detail/12345",
      "source": "Dice",
      "salary": "$200,000 - $250,000",
      "description": "Lead .NET architecture initiatives...",
      "postedDate": "2025-11-01",
      "jobType": "Full-time"
    }
  ],
  "jobCount": 23,
  "googleDiscoveryCount": 5,
  "duration": 45.3,
  "errors": []
}
```
