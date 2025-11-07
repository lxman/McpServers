# Sync Data

Synchronizes data from a source server collection to a target server collection, with optional filtering and dry-run mode.

## Parameters

- **sourceServer** (string, required): The name of the source server connection
- **targetServer** (string, required): The name of the target server connection
- **collectionName** (string, required): The name of the collection to sync
- **filterJson** (string, optional): JSON string representing a filter to limit sync scope (default: "{}")
- **dryRun** (boolean, required): If true, simulates the sync without making changes

## Returns

Returns a JSON object with sync results:

```json
{
  "success": true,
  "sourceServer": "production",
  "targetServer": "staging",
  "collectionName": "users",
  "dryRun": false,
  "stats": {
    "documentsScanned": 1500,
    "inserted": 25,
    "updated": 50,
    "deleted": 5,
    "unchanged": 1420
  },
  "duration": 2500
}
```

## Example

Dry-run sync from production to staging:

```
sourceServer: "production"
targetServer: "staging"
collectionName: "users"
filterJson: "{}"
dryRun: true
```

Sync only active users:

```
sourceServer: "production"
targetServer: "staging"
collectionName: "users"
filterJson: "{\"status\": \"active\"}"
dryRun: false
```
