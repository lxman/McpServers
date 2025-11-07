# Connect

Establishes a connection to a Redis server using the provided connection string.

## Parameters

- **connectionString** (string, required): The Redis connection string in the format `host:port` or a full connection string with authentication (e.g., `password@host:port` or `host:port,password=mypassword`)

## Returns

Returns a JSON object containing the connection result:

```json
{
  "success": true,
  "message": "Successfully connected to Redis at localhost:6379",
  "serverVersion": "7.0.0"
}
```

If the connection fails:

```json
{
  "success": false,
  "error": "Failed to connect: Connection refused"
}
```

## Example

Connect to a local Redis server:
```
connectionString: localhost:6379
```

Connect to a Redis server with authentication:
```
connectionString: mypassword@redis.example.com:6379
```

Connect using a full connection string:
```
connectionString: redis.example.com:6379,password=mySecurePassword,ssl=true
```
