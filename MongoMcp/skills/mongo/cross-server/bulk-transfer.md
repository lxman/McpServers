# Bulk Transfer

Transfers multiple collections from a source server to a target server in a single operation.

## Parameters

- **sourceServer** (string, required): The name of the source server connection
- **targetServer** (string, required): The name of the target server connection
- **collectionNames** (array of strings, required): Array of collection names to transfer
- **dryRun** (boolean, required): If true, simulates the transfer without making changes

## Returns

Returns a JSON object with bulk transfer results:

```json
{
  "success": true,
  "sourceServer": "backup",
  "targetServer": "production",
  "dryRun": false,
  "results": [
    {
      "collectionName": "users",
      "success": true,
      "documentsTransferred": 1500,
      "duration": 2500
    },
    {
      "collectionName": "orders",
      "success": true,
      "documentsTransferred": 5000,
      "duration": 8500
    }
  ],
  "totalCollections": 2,
  "successfulTransfers": 2,
  "failedTransfers": 0,
  "totalDuration": 11000
}
```

## Example

Transfer multiple collections from backup to production:

```
sourceServer: "backup"
targetServer: "production"
collectionNames: ["users", "orders", "products", "categories"]
dryRun: false
```

Dry-run test transfer:

```
sourceServer: "development"
targetServer: "staging"
collectionNames: ["test_data"]
dryRun: true
```
