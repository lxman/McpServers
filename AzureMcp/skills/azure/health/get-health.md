# Get Health

Get Azure service health status and check availability of all Azure services.

## Parameters
None

## Returns
JSON object with success status, overall health status, and detailed service availability.

## Example Response
```json
{
  "success": true,
  "status": "Healthy",
  "timestamp": "2025-01-15T10:30:00Z",
  "services": {
    "credentials": {
      "serviceName": "Azure Credentials",
      "isAvailable": true,
      "status": "Selected"
    },
    "arm-client": {
      "serviceName": "Azure Resource Manager",
      "isAvailable": true,
      "status": "Available"
    }
  },
  "availableServices": 15,
  "totalServices": 15,
  "healthPercentage": 100
}
```
