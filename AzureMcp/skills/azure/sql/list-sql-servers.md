# List SQL Servers

List all Azure SQL servers in subscription or resource group.

## Parameters
- **subscriptionId** (string, optional): Azure subscription ID
- **resourceGroupName** (string, optional): Resource group name

## Returns
JSON array of SQL server objects with name, location, and properties.

## Example Response
```json
{
  "success": true,
  "servers": [
    {
      "name": "myserver",
      "location": "eastus",
      "sku": "Standard"
    }
  ]
}
```