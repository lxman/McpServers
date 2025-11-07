# health_check

Check the health status of the SeleniumMcp service and all connected job sites.

## Parameters

No parameters required for this operation.

## Returns

JSON string containing:
- **success** (boolean): Whether the health check completed successfully
- **serviceStatus** (string): Overall service status. Options: 'healthy', 'degraded', 'unhealthy'
- **uptime** (string): Service uptime duration
- **version** (string): Service version
- **sitesStatus** (object): Status of each job site
  - **site** (string): Site name
  - **accessible** (boolean): Whether site is currently accessible
  - **lastScraped** (string): When site was last scraped
  - **responseTime** (number, optional): Response time in milliseconds
  - **issues** (array, optional): Any issues with this site
- **storageStatus** (object, optional): MongoDB storage status
  - **connected** (boolean): Is storage connected
  - **jobCount** (number): Total jobs in storage
  - **lastUpdate** (string): Last database update
  - **diskUsage** (string, optional): Disk space used
- **memoryUsage** (object): Memory usage metrics
  - **used** (number): Memory used in MB
  - **available** (number): Memory available in MB
  - **percentage** (number): Percentage of available memory used
- **errors** (array, optional): List of any errors encountered

## Example

```json
{
  "success": true,
  "serviceStatus": "healthy",
  "uptime": "14 days 3 hours",
  "version": "1.0.0",
  "sitesStatus": {
    "Dice": {
      "accessible": true,
      "lastScraped": "2025-11-06T14:32:00Z",
      "responseTime": 1247,
      "issues": []
    },
    "BuiltIn": {
      "accessible": true,
      "lastScraped": "2025-11-06T14:30:00Z",
      "responseTime": 892,
      "issues": []
    },
    "SimplifyJobs": {
      "accessible": true,
      "lastScraped": "2025-11-06T14:28:00Z",
      "responseTime": 1543,
      "issues": []
    }
  },
  "storageStatus": {
    "connected": true,
    "jobCount": 2847,
    "lastUpdate": "2025-11-06T14:35:00Z",
    "diskUsage": "1.2 GB"
  },
  "memoryUsage": {
    "used": 512,
    "available": 1024,
    "percentage": 50
  },
  "errors": []
}
```
