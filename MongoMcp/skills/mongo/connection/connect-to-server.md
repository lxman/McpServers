# Connect to Server

Establishes a connection to a MongoDB server instance with the specified connection string and database name.

## Parameters

- **serverName** (string, required): A unique name to identify this connection in subsequent operations
- **connectionString** (string, required): MongoDB connection string (e.g., "mongodb://localhost:27017" or "mongodb+srv://user:pass@cluster.mongodb.net")
- **databaseName** (string, required): The default database to use for this connection

## Returns

Returns a JSON object with connection status:

```json
{
  "success": true,
  "serverName": "myserver",
  "databaseName": "mydb",
  "message": "Successfully connected to MongoDB server"
}
```

## Example

Connect to a local MongoDB instance:

```
serverName: "local"
connectionString: "mongodb://localhost:27017"
databaseName: "myapp"
```

Connect to MongoDB Atlas:

```
serverName: "production"
connectionString: "mongodb+srv://admin:password@cluster0.mongodb.net"
databaseName: "production_db"
```
