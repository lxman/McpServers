# Get Info

Retrieves detailed information and statistics about the Redis server. This includes server version, memory usage, connected clients, persistence info, replication status, and more.

## Parameters

- **section** (string, optional): Specific section of information to retrieve. If omitted, returns all sections. Available sections include:
  - `server`: General server information
  - `clients`: Connected clients information
  - `memory`: Memory consumption details
  - `persistence`: RDB and AOF persistence information
  - `stats`: General statistics
  - `replication`: Master/replica replication information
  - `cpu`: CPU usage statistics
  - `keyspace`: Database key statistics
  - `cluster`: Redis Cluster information
  - `commandstats`: Command execution statistics
  - `all`: All sections (default)
  - `default`: Default set of sections

## Returns

Returns a JSON object with the requested server information:

```json
{
  "success": true,
  "section": "server",
  "info": {
    "redis_version": "7.0.0",
    "redis_mode": "standalone",
    "os": "Linux 5.10.0-21-amd64 x86_64",
    "arch_bits": "64",
    "process_id": "1234",
    "uptime_in_seconds": "86400",
    "uptime_in_days": "1"
  }
}
```

Memory section example:

```json
{
  "success": true,
  "section": "memory",
  "info": {
    "used_memory": "1048576",
    "used_memory_human": "1.00M",
    "used_memory_peak": "2097152",
    "used_memory_peak_human": "2.00M",
    "maxmemory": "0",
    "maxmemory_policy": "noeviction"
  }
}
```

Keyspace section example:

```json
{
  "success": true,
  "section": "keyspace",
  "info": {
    "db0": "keys=1000,expires=200,avg_ttl=3600000",
    "db1": "keys=500,expires=50,avg_ttl=7200000"
  }
}
```

If the operation fails:

```json
{
  "success": false,
  "error": "Failed to retrieve server info: Connection error"
}
```

## Example

Get all server information:
```
(no parameters - returns all sections)
```

Get server version and uptime:
```
section: server
```

Get memory usage:
```
section: memory
```

Get database statistics:
```
section: keyspace
```

Get replication status:
```
section: replication
```

Get client connections:
```
section: clients
```

Get command statistics:
```
section: commandstats
```

This is useful for:
- Monitoring Redis server health
- Tracking memory usage and detecting leaks
- Checking database key counts
- Debugging performance issues
- Verifying replication status
- Capacity planning
