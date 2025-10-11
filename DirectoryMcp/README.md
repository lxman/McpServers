# DirectoryMcp - MCP Metaserver

A lightweight Model Context Protocol (MCP) metaserver that provides a registry of HTTP-based MCP server APIs, dramatically reducing token consumption.

## The Problem

When using multiple MCP servers with Claude, each server's complete tool schema (descriptions, parameters, types, etc.) is loaded into the conversation context. With hundreds of tools across multiple servers (AWS, Azure, MongoDB, etc.), this can consume **70,000+ tokens** before your conversation even starts.

## The Solution

**DirectoryMcp** acts as a minimal "phone book" that:
- Exposes a single MCP tool: `list_servers()`
- Returns a JSON registry of available server URLs
- Reduces upfront token cost from ~70k to ~500 tokens
- Lets Claude fetch tool schemas on-demand when needed

### Token Savings Example
- **Before**: 96k tokens (all tools loaded upfront)
- **After**: ~500 tokens (just the directory)
- **Savings**: 95.5k tokens (~50% of your context budget!)

## Architecture

```
Claude (STDIO/MCP)
    ↓
DirectoryMcp (STDIO MCP Server - this project)
    ↓ (provides URLs for)
    ├─→ AWS API (HTTP - localhost:5001)
    ├─→ Azure API (HTTP - localhost:5002)
    └─→ Desktop Commander API (HTTP - localhost:5003)
```

## How It Works

1. **Claude calls**: `list_servers()` via STDIO/MCP
2. **DirectoryMcp returns**: JSON with server names and URLs
3. **Claude calls directly**: `GET https://localhost:5001/description` to get AWS tool schemas
4. **Claude executes tools**: `POST https://localhost:5001/execute/s3_list_buckets` with parameters

After step 1, DirectoryMcp's job is done. All subsequent communication is direct HTTP between Claude and your API servers.

## Configuration

Edit `servers.json` to define which servers you want to expose:

```json
{
  "servers": {
    "aws": {
      "name": "AWS Tools",
      "url": "https://localhost:5001"
    },
    "azure": {
      "name": "Azure Tools",
      "url": "https://localhost:5002"
    },
    "desktop_commander": {
      "name": "Desktop Commander",
      "url": "https://localhost:5003"
    }
  },
  "usage": "Call GET {url}/description to see available endpoints and capabilities."
}
```

### Configuration Properties

- **servers**: Dictionary of server IDs to server info
    - **name**: Human-readable server name
    - **url**: Base URL for the HTTP API
- **usage**: Instructions for Claude on how to use the URLs

### Customization

**Want only AWS and Azure?**
1. Start only those two API servers on their respective ports
2. Edit `servers.json` to include only those entries
3. Run DirectoryMcp

**Need different ports?**
Just update the URLs in `servers.json` - no code changes needed!

## Building and Running

### Prerequisites
- .NET 9.0 or later
- ModelContextProtocol package (0.4.0-preview.2)

### Build
```bash
dotnet build
```

### Run
```bash
dotnet run
```

The server will:
1. Load `servers.json` from the output directory
2. Start listening on STDIO for MCP requests
3. Log to stderr (STDOUT is reserved for MCP protocol)

## API Contract for Backend Servers

Your HTTP-based MCP servers should expose:

### GET /description
Returns all available tools with their schemas:
```json
{
  "tools": [
    {
      "name": "s3_list_buckets",
      "description": "List all S3 buckets",
      "parameters": { ... }
    }
  ]
}
```

### POST /execute/{tool_name}
Executes a specific tool with the provided parameters:
```json
{
  "parameters": {
    "region": "us-east-1"
  }
}
```

Returns the tool execution result.

## Expected Output

When Claude calls `list_servers()`, it receives:

```json
{
  "servers": {
    "aws": {
      "name": "AWS Tools",
      "url": "https://localhost:5001"
    },
    "azure": {
      "name": "Azure Tools",
      "url": "https://localhost:5002"
    }
  },
  "usage": "Call GET {url}/description to see available endpoints and capabilities."
}
```

## Error Handling

If `servers.json` is missing or invalid, the tool returns:
```json
{
  "error": "Configuration file not found",
  "expectedPath": "/path/to/servers.json",
  "message": "Create a servers.json file with your server configuration"
}
```

## Benefits

✅ **Massive token savings** - 70k+ tokens freed up  
✅ **Flexible configuration** - Edit JSON, no code changes  
✅ **Selective loading** - Only start the servers you need  
✅ **Version independence** - Tool counts don't need updating  
✅ **Simple architecture** - Just a registry, minimal complexity  
✅ **Direct communication** - No proxy overhead after discovery

## Next Steps

1. Convert your existing MCP servers to HTTP APIs
2. Configure `servers.json` with their URLs
3. Start the API servers
4. Run DirectoryMcp
5. Enjoy your reclaimed tokens!

## License

[Your License Here]