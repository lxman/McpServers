# SerilogFileWriter - Usage Examples

## Example 1: Minimal Setup (Recommended)

The absolute minimum code needed for MCP server logging:

```csharp
using Serilog;
using SerilogFileWriter;

public static async Task Main(string[] args)
{
    Log.Logger = McpLoggingExtensions.SetupMcpLogging("logs/mcp-server-.log");

    try
    {
        await McpServer.RunAsync(args); // Your MCP server entry point
    }
    finally
    {
        await Log.CloseAndFlushAsync();
    }
}
```

**That's it!** Console.WriteLine, ILogger<T>, and Log.xxx all go to one file.

---

## Example 2: Production Configuration

Production-ready setup with appropriate log levels:

```csharp
using Serilog;
using Serilog.Events;
using SerilogFileWriter;

public static async Task Main(string[] args)
{
    // Production: Information level, daily rolling logs
    Log.Logger = McpLoggingExtensions.SetupMcpLogging(
        logFilePath: "logs/mcp-server-.log",
        minimumLevel: LogEventLevel.Information,
        rollingInterval: RollingInterval.Day);

    try
    {
        Log.Information("MCP Server v{Version} starting in {Environment}",
            Assembly.GetExecutingAssembly().GetName().Version,
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"));

        var host = Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureServices(services =>
            {
                services.AddMcpServer();
            })
            .Build();

        await host.RunAsync();
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "Application terminated unexpectedly");
        throw;
    }
    finally
    {
        Log.Information("Shutting down...");
        await Log.CloseAndFlushAsync();
    }
}
```

---

## Example 3: Development with Debug Logging

Development setup with verbose logging:

```csharp
using Serilog;
using Serilog.Events;
using SerilogFileWriter;

public static async Task Main(string[] args)
{
    // Development: Debug level, hourly rolling (more granular)
    Log.Logger = McpLoggingExtensions.SetupMcpLogging(
        logFilePath: "logs/mcp-server-.log",
        minimumLevel: LogEventLevel.Debug,
        rollingInterval: RollingInterval.Hour);

    Log.Debug("Starting in DEBUG mode - verbose logging enabled");

    try
    {
        var host = Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureServices(services =>
            {
                services.AddMcpServer();
            })
            .Build();

        await host.RunAsync();
    }
    finally
    {
        await Log.CloseAndFlushAsync();
    }
}
```

---

## Example 4: Custom Configuration

Advanced setup with custom Serilog configuration:

```csharp
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;
using SerilogFileWriter;

public static async Task Main(string[] args)
{
    // Custom configuration
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .MinimumLevel.Override("System", LogEventLevel.Warning)
        .Enrich.WithThreadId()
        .Enrich.WithMachineName()
        .Enrich.WithEnvironmentName()
        .WriteTo.File(
            "logs/mcp-server-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30, // Keep 30 days
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{ThreadId}] {SourceContext} {Message:lj}{NewLine}{Exception}")
        .WriteTo.File(
            new JsonFormatter(),
            "logs/mcp-server-.json", // Also write JSON logs
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7)
        .CreateLogger();

    // Still redirect console
    Log.Logger.RedirectConsoleToSerilog();

    try
    {
        await McpServer.RunAsync(args);
    }
    finally
    {
        await Log.CloseAndFlushAsync();
    }
}
```

---

## Example 5: Environment-Based Configuration

Different configs for Development vs Production:

```csharp
using Serilog;
using Serilog.Events;
using SerilogFileWriter;

public static async Task Main(string[] args)
{
    var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
    var isDevelopment = environment.Equals("Development", StringComparison.OrdinalIgnoreCase);

    // Configure based on environment
    Log.Logger = McpLoggingExtensions.SetupMcpLogging(
        logFilePath: $"logs/mcp-server-{environment.ToLower()}-.log",
        minimumLevel: isDevelopment ? LogEventLevel.Debug : LogEventLevel.Information,
        rollingInterval: isDevelopment ? RollingInterval.Hour : RollingInterval.Day);

    Log.Information("Starting in {Environment} mode", environment);

    try
    {
        var host = Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureServices(services =>
            {
                services.AddMcpServer();
            })
            .Build();

        await host.RunAsync();
    }
    finally
    {
        await Log.CloseAndFlushAsync();
    }
}
```

---

## Example 6: Service with ILogger<T> Injection

How to use ILogger<T> in your services:

```csharp
using Microsoft.Extensions.Logging;

public class MyMcpToolService
{
    private readonly ILogger<MyMcpToolService> _logger;

    public MyMcpToolService(ILogger<MyMcpToolService> logger)
    {
        _logger = logger;
    }

    public async Task<string> ExecuteToolAsync(string input)
    {
        _logger.LogDebug("ExecuteTool called with input: {Input}", input);

        try
        {
            // Do work...
            var result = await DoSomethingAsync(input);

            _logger.LogInformation("Tool executed successfully: {Result}", result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool execution failed for input: {Input}", input);
            throw;
        }
    }

    private async Task<string> DoSomethingAsync(string input)
    {
        // This will also be captured in the log
        Console.WriteLine($"Processing: {input}");

        await Task.Delay(100);
        return $"Processed: {input}";
    }
}
```

---

## Example 7: Testing Console Redirection

Verify that Console.WriteLine is being captured:

```csharp
using Serilog;
using SerilogFileWriter;

public static async Task Main(string[] args)
{
    Log.Logger = McpLoggingExtensions.SetupMcpLogging("logs/test.log");

    // All of these go to the same log file:
    Log.Information("Direct Serilog call");
    Console.WriteLine("Console.WriteLine call");
    Console.Error.WriteLine("Console.Error.WriteLine call");

    // With ILogger<T>
    var loggerFactory = LoggerFactory.Create(builder =>
    {
        builder.AddSerilog(Log.Logger);
    });
    var logger = loggerFactory.CreateLogger<Program>();
    logger.LogInformation("ILogger<T> call");

    await Log.CloseAndFlushAsync();

    // Check logs/test.log to see all four messages
}
```

Expected output in `logs/test.log`:
```
2025-11-08 02:30:45.123 -05:00 [INF]  Direct Serilog call
2025-11-08 02:30:45.124 -05:00 [INF]  [Console] Console.WriteLine call
2025-11-08 02:30:45.125 -05:00 [ERR]  [Console] Console.Error.WriteLine call
2025-11-08 02:30:45.126 -05:00 [INF] Program ILogger<T> call
```

---

## Example 8: Different Log Levels for Console

Customize what log level Console writes use:

```csharp
using Serilog;
using Serilog.Events;
using SerilogFileWriter;

public static async Task Main(string[] args)
{
    Log.Logger = new LoggerConfiguration()
        .WriteTo.File("logs/server.log")
        .CreateLogger();

    // Console.Out as Debug level (less important)
    // Console.Error as Fatal level (very important)
    Log.Logger.RedirectConsoleToSerilog(
        consoleOutLevel: LogEventLevel.Debug,
        consoleErrorLevel: LogEventLevel.Fatal);

    Console.WriteLine("This is Debug level");
    Console.Error.WriteLine("This is Fatal level");

    await Log.CloseAndFlushAsync();
}
```

---

## Example 9: Structured Logging Best Practices

How to do structured logging properly:

```csharp
public class UserService
{
    private readonly ILogger<UserService> _logger;

    public UserService(ILogger<UserService> logger)
    {
        _logger = logger;
    }

    public async Task ProcessUserAsync(int userId, string action)
    {
        // ✅ GOOD: Structured logging with properties
        _logger.LogInformation("Processing user action: {Action} for user {UserId}",
            action, userId);

        // ❌ BAD: String interpolation loses structure
        // _logger.LogInformation($"Processing user action: {action} for user {userId}");

        try
        {
            await DoProcessingAsync(userId, action);

            // ✅ GOOD: Include relevant context
            _logger.LogInformation(
                "User action completed successfully: {Action} for {UserId} in {Duration}ms",
                action, userId, 123);
        }
        catch (Exception ex)
        {
            // ✅ GOOD: Log exception with context
            _logger.LogError(ex,
                "Failed to process user action: {Action} for {UserId}",
                action, userId);
            throw;
        }
    }
}
```

---

## Example 10: Migration from Existing MCP Server

If you have an existing MCP server without proper logging:

**Before:**
```csharp
public static async Task Main(string[] args)
{
    // Logs might go to console and break MCP protocol
    var host = Host.CreateDefaultBuilder(args)
        .ConfigureServices(services =>
        {
            services.AddMcpServer();
        })
        .Build();

    await host.RunAsync();
}
```

**After:**
```csharp
using Serilog;
using SerilogFileWriter;

public static async Task Main(string[] args)
{
    // Add these 3 lines:
    Log.Logger = McpLoggingExtensions.SetupMcpLogging("logs/mcp-server-.log");

    try
    {
        var host = Host.CreateDefaultBuilder(args)
            .UseSerilog() // Add this line
            .ConfigureServices(services =>
            {
                services.AddMcpServer();
            })
            .Build();

        await host.RunAsync();
    }
    finally
    {
        await Log.CloseAndFlushAsync(); // Add this line
    }
}
```

That's it! Three lines of code and your MCP server is protected.

---

## Common Patterns

### Pattern 1: Early Initialization

Always set up logging as early as possible:

```csharp
public static async Task Main(string[] args)
{
    // FIRST: Set up logging
    Log.Logger = McpLoggingExtensions.SetupMcpLogging("logs/server-.log");

    // THEN: Log startup
    Log.Information("Application starting...");

    // THEN: Do everything else
    try
    {
        await RunServerAsync(args);
    }
    finally
    {
        await Log.CloseAndFlushAsync();
    }
}
```

### Pattern 2: Graceful Shutdown

Always flush logs on shutdown:

```csharp
public static async Task Main(string[] args)
{
    Log.Logger = McpLoggingExtensions.SetupMcpLogging("logs/server-.log");

    try
    {
        await host.RunAsync();
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "Application crashed");
        throw;
    }
    finally
    {
        Log.Information("Application shutting down");
        await Log.CloseAndFlushAsync(); // Critical!
    }
}
```

### Pattern 3: Feature Flags in Logging

Control logging behavior with configuration:

```csharp
public static async Task Main(string[] args)
{
    var config = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .AddEnvironmentVariables()
        .Build();

    var logLevel = config.GetValue<LogEventLevel>("Logging:MinimumLevel", LogEventLevel.Information);
    var logPath = config.GetValue<string>("Logging:FilePath", "logs/server-.log");

    Log.Logger = McpLoggingExtensions.SetupMcpLogging(logPath, logLevel);

    // ... rest of setup
}
```

## Troubleshooting Tips

### Tip 1: Verify Console Redirection

Test that console redirection is working:

```csharp
Log.Logger = McpLoggingExtensions.SetupMcpLogging("logs/test.log");

Console.WriteLine("TEST: If you see this in the log file, redirection works!");

await Log.CloseAndFlushAsync();

// Check logs/test.log - should see: [INF]  [Console] TEST: If you see this...
```

### Tip 2: Check Log File Location

If logs aren't appearing, verify the path:

```csharp
var logPath = Path.Combine(Directory.GetCurrentDirectory(), "logs", "server-.log");
Console.WriteLine($"Logging to: {logPath}"); // This will be in the log file!

Log.Logger = McpLoggingExtensions.SetupMcpLogging(logPath);
```

### Tip 3: Debug Log Level Issues

If you're not seeing expected logs:

```csharp
// Temporarily set to Debug to see everything
Log.Logger = McpLoggingExtensions.SetupMcpLogging(
    "logs/debug.log",
    minimumLevel: LogEventLevel.Verbose); // See ALL logs

// ... investigate ...

// Then return to Information for production
```
