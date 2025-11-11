# Mcp.ResponseGuard

A comprehensive library for standardizing MCP tool responses, protecting against oversized responses, and providing consistent error handling across all MCP servers.

## Features

- ✅ **Response Size Protection**: Automatically checks and guards against responses that exceed MCP protocol limits
- ✅ **Standardized Response Formats**: Consistent success, error, and oversized response structures
- ✅ **Configurable Limits**: Adjust token limits and behavior per server
- ✅ **Easy to Use**: Extension methods and fluent API
- ✅ **Logging**: Built-in logging for size violations and errors
- ✅ **Type-Safe**: Full nullable reference type support

## Installation

Add a project reference to your MCP server:

```xml
<ProjectReference Include="..\Mcp.ResponseGuard\Mcp.ResponseGuard.csproj" />
```

## Quick Start

### 1. Register OutputGuard in DI Container

```csharp
// In Program.cs
builder.Services.AddSingleton<OutputGuard>();

// Or with custom options:
builder.Services.AddSingleton(sp => new OutputGuard(
    sp.GetRequiredService<ILogger<OutputGuard>>(),
    new OutputGuardOptions
    {
        SafeTokenLimit = 15_000,  // Lower limit for query-heavy servers
        AutoTruncate = false
    }
));
```

### 2. Inject in Your Tool Class

```csharp
[McpServerToolType]
public class MyTools(
    OutputGuard outputGuard,
    ILogger<MyTools> logger)
{
    [McpServerTool, DisplayName("my_tool")]
    public async Task<string> MyTool(string param)
    {
        try
        {
            var result = await DoWork(param);

            // Simple approach - automatic size checking
            return result.ToGuardedResponse(outputGuard, "my_tool");
        }
        catch (Exception ex)
        {
            return ex.ToErrorResponse(outputGuard);
        }
    }
}
```

## Usage Patterns

### Pattern 1: Extension Methods (Recommended)

```csharp
// Success with automatic size checking
return result.ToGuardedResponse(outputGuard, "tool_name");

// Success with custom oversized suggestion
return result.ToGuardedResponse(
    outputGuard,
    "tool_name",
    "Try using the 'limit' parameter to reduce results");

// Error from exception
return ex.ToErrorResponse(outputGuard);

// Error from string
return "File not found".ToErrorResponse(
    outputGuard,
    details: new { path = filePath },
    suggestion: "Check the file path and try again",
    errorCode: "FILE_NOT_FOUND");

// Simple success wrapper
return data.ToSuccessResponse(outputGuard, "Operation completed successfully");
```

### Pattern 2: Manual Control

```csharp
// Check size first, then decide
ResponseSizeCheck check = outputGuard.CheckResponseSize(result, "tool_name");

if (!check.IsWithinLimit)
{
    return outputGuard.CreateOversizedErrorResponse(
        check,
        $"Query returned {rowCount} rows",
        "Try adding a WHERE clause or reducing maxRows parameter",
        new { rowCount, currentMaxRows = 1000, suggestedMaxRows = 100 });
}

return check.SerializedJson!;
```

### Pattern 3: Standardized Error Responses

```csharp
// From exception
return outputGuard.CreateErrorResponse(
    exception,
    suggestion: "Ensure the database connection is valid",
    errorCode: "DB_CONNECTION_FAILED");

// From message
return outputGuard.CreateErrorResponse(
    "Invalid parameter value",
    details: new { parameter = "maxRows", value = maxRows, allowedRange = "1-1000" },
    suggestion: "Provide a value between 1 and 1000",
    errorCode: "INVALID_PARAMETER");
```

## Response Formats

### Success Response

```json
{
  "success": true,
  "data": {
    // Your data here
  },
  "message": "Optional success message"
}
```

### Error Response

```json
{
  "success": false,
  "error": "Primary error message",
  "details": {
    // Additional error details
  },
  "suggestion": "How to resolve the issue",
  "errorCode": "ERROR_CODE"
}
```

### Oversized Response

```json
{
  "success": false,
  "error": "Response too large",
  "message": "The response from tool_name is too large...",
  "details": {
    "characterCount": 150000,
    "estimatedTokens": 37500,
    "safeTokenLimit": 20000,
    "hardTokenLimit": 25000,
    "percentOfLimit": 187.5
  },
  "suggestion": "Try reducing maxRows parameter",
  "metrics": {
    // Tool-specific metrics
  }
}
```

## Configuration Options

```csharp
var options = new OutputGuardOptions
{
    // Safe limit accounting for MCP overhead (default: 20,000)
    SafeTokenLimit = 20_000,

    // Hard MCP protocol limit (default: 25,000)
    HardTokenLimit = 25_000,

    // Characters per token estimate (default: 4)
    CharsPerToken = 4,

    // Auto-truncate instead of error (default: false)
    AutoTruncate = false,

    // If auto-truncate, target percentage (default: 90%)
    TruncateToPercentage = 0.9
};
```

### Recommended Limits by Server Type

| Server Type | Safe Limit | Reason |
|-------------|-----------|---------|
| Query-heavy (SQL, MongoDB) | 15,000 | Large result sets common |
| Document processing | 18,000 | Large text content possible |
| File operations | 20,000 | Usually smaller responses |
| System operations | 20,000 | Control responses |

## Migration Guide

### From DesktopCommander.Core.ResponseSizeGuard

**Before:**
```csharp
public class MyTools(
    ResponseSizeGuard responseSizeGuard,
    ILogger<MyTools> logger)
{
    public async Task<string> MyTool()
    {
        var result = await GetData();
        ResponseSizeCheck check = responseSizeGuard.CheckResponseSize(result, "my_tool");

        if (!check.IsWithinLimit)
        {
            return ResponseSizeGuard.CreateOversizedErrorResponse(check, "...", "...");
        }

        return check.SerializedJson!;
    }
}
```

**After:**
```csharp
using Mcp.ResponseGuard.Services;
using Mcp.ResponseGuard.Extensions;

public class MyTools(
    OutputGuard outputGuard,  // Changed type
    ILogger<MyTools> logger)
{
    public async Task<string> MyTool()
    {
        try
        {
            var result = await GetData();
            return result.ToGuardedResponse(outputGuard, "my_tool");  // Simplified!
        }
        catch (Exception ex)
        {
            return ex.ToErrorResponse(outputGuard);  // Standardized errors!
        }
    }
}
```

### From SqlServer.Core.ResponseSizeGuard

Same migration pattern as above. Note: SqlServer.Core used 15k token limit, so configure:

```csharp
// In Program.cs
builder.Services.AddSingleton(sp => new OutputGuard(
    sp.GetRequiredService<ILogger<OutputGuard>>(),
    new OutputGuardOptions { SafeTokenLimit = 15_000 }
));
```

## Benefits

1. **Consistency**: All MCP tools return responses in the same format
2. **Safety**: Automatic protection against protocol violations
3. **Observability**: Built-in logging for size issues
4. **Maintainability**: Single place to update response handling
5. **Ease of Use**: Extension methods reduce boilerplate
6. **Type Safety**: Full nullable reference type support
7. **Flexibility**: Configurable per server or per tool

## Advanced Usage

### Custom Metrics in Oversized Responses

```csharp
var check = outputGuard.CheckResponseSize(result, "query_tool");

if (!check.IsWithinLimit)
{
    return outputGuard.CreateOversizedErrorResponse(
        check,
        $"Query returned {rows.Count} rows",
        "Reduce maxRows or add WHERE clause",
        additionalMetrics: new
        {
            rowsReturned = rows.Count,
            columnsReturned = columns.Count,
            avgRowSize = check.CharacterCount / rows.Count,
            suggestedMaxRows = 100
        });
}
```

### Pre-flight Size Estimation

```csharp
// Estimate before expensive serialization
int estimatedChars = items.Count * 500;  // Rough estimate per item

if (outputGuard.WouldExceedLimit(estimatedChars))
{
    return "Too many items".ToErrorResponse(
        outputGuard,
        details: new { itemCount = items.Count, limit = 1000 },
        suggestion: "Reduce the number of items requested");
}
```

## Testing

```csharp
// Create with custom options for testing
var testLogger = new NullLogger<OutputGuard>();
var testGuard = new OutputGuard(testLogger, new OutputGuardOptions
{
    SafeTokenLimit = 100,  // Low limit for testing
    HardTokenLimit = 150
});

// Test oversized response
var largeData = new string('x', 500);
string result = largeData.ToGuardedResponse(testGuard, "test_tool");

// Verify error response
Assert.Contains("Response too large", result);
```

## See Also

- [MCP Protocol Documentation](https://modelcontextprotocol.io/)
- [Mcp.Common.Core](../Mcp.Common.Core/README.md) - Shared MCP utilities
