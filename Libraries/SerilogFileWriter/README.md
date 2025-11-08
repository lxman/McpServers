# SerilogFileWriter Library

A lightweight library that provides **complete STDOUT/STDERR isolation** for MCP (Model Context Protocol) servers by redirecting all Console output to Serilog.

## Problem

MCP servers use STDIO for JSON-RPC protocol communication. Any output to `Console.WriteLine` or logging to console sinks will corrupt the protocol messages, causing the MCP client to hang or fail.

## Solution

The `SerilogTextWriter` class intercepts all Console writes and routes them through Serilog to file-based logging, ensuring:
- ✅ **Complete STDOUT/STDERR isolation** - MCP protocol remains clean
- ✅ **Single unified log file** - All logging sources in one place
- ✅ **Zero lost messages** - Captures Console.WriteLine, ILogger<T>, and direct Serilog calls
- ✅ **Thread-safe** - Handles concurrent logging correctly
- ✅ **Source identification** - `[Console]` prefix shows origin

## Quick Start

### Option 1: One-Line Setup (Easiest)

```csharp
using Serilog;
using SerilogFileWriter;

public static async Task Main(string[] args)
{
    // One-line setup - creates logger, redirects console, done!
    Log.Logger = McpLoggingExtensions.SetupMcpLogging("logs/mcp-server-.log");

    try
    {
        var host = Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureServices(services => services.AddMcpServer())
            .Build();

        await host.RunAsync();
    }
    finally
    {
        await Log.CloseAndFlushAsync();
    }
}
```

### Option 2: Manual Setup (More Control)

```csharp
using Serilog;
using SerilogFileWriter;

public static async Task Main(string[] args)
{
    // Configure Serilog
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .WriteTo.File("logs/mcp-server-.log",
            rollingInterval: RollingInterval.Day)
        .CreateLogger();

    // Redirect Console to Serilog
    Log.Logger.RedirectConsoleToSerilog();

    try
    {
        // Your MCP server setup...
        var host = Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureServices(services => services.AddMcpServer())
            .Build();

        await host.RunAsync();
    }
    finally
    {
        await Log.CloseAndFlushAsync();
    }
}
```

### Option 3: Helper Configuration

```csharp
using Serilog;
using SerilogFileWriter;

public static async Task Main(string[] args)
{
    // Use the helper to create optimized MCP logger configuration
    Log.Logger = McpLoggingExtensions
        .CreateMcpLoggerConfiguration(
            logFilePath: "logs/mcp-server-.log",
            minimumLevel: LogEventLevel.Information,
            rollingInterval: RollingInterval.Day)
        .CreateLogger();

    // Redirect console
    Log.Logger.RedirectConsoleToSerilog();

    // ... rest of your setup
}
```

## Installation

Add a project reference to this library:

```xml
<ItemGroup>
  <ProjectReference Include="..\Libraries\SerilogFileWriter\SerilogFileWriter.csproj" />
</ItemGroup>
```

## Features

### SerilogTextWriter

The core class that implements `TextWriter` to intercept Console writes:

- **Thread-safe buffering** - Uses lock to prevent concurrent write issues
- **Immediate flushing** - Flushes on newline characters
- **Preserves log levels** - Console.Out → Information, Console.Error → Error
- **[Console] prefix** - Identifies Console writes in the log

### Extension Methods

#### `RedirectConsoleToSerilog()`

Redirects Console.Out and Console.Error to Serilog:

```csharp
Log.Logger.RedirectConsoleToSerilog();

// Optional: customize log levels
Log.Logger.RedirectConsoleToSerilog(
    consoleOutLevel: LogEventLevel.Debug,
    consoleErrorLevel: LogEventLevel.Fatal);
```

#### `CreateMcpLoggerConfiguration()`

Creates a pre-configured `LoggerConfiguration` optimized for MCP servers:

```csharp
var config = McpLoggingExtensions.CreateMcpLoggerConfiguration(
    logFilePath: "logs/server.log",
    minimumLevel: LogEventLevel.Information,
    rollingInterval: RollingInterval.Hour);

Log.Logger = config.CreateLogger();
```

Features:
- File sink with rolling interval
- Microsoft/System framework logs set to Warning level
- Structured logging with timestamps
- 1-second flush interval

#### `SetupMcpLogging()`

Complete one-line setup:

```csharp
Log.Logger = McpLoggingExtensions.SetupMcpLogging("logs/mcp-server-.log");
// Done! Console is redirected and logger is configured
```

## Log Output Format

All logs are written with this format:

```
2025-11-08 02:30:45.123 -05:00 [INF] MyNamespace.MyClass Message text here
2025-11-08 02:30:45.124 -05:00 [INF]  [Console] This came from Console.WriteLine
2025-11-08 02:30:45.125 -05:00 [ERR]  [Console] This came from Console.Error.WriteLine
```

The `[Console]` prefix makes it easy to identify which messages came from Console writes versus structured logging.

## Best Practices

### ✅ DO

1. ✅ Call `SetupMcpLogging()` or `RedirectConsoleToSerilog()` early in `Main()`
2. ✅ Use `await Log.CloseAndFlushAsync()` in finally block
3. ✅ Use `.UseSerilog()` on your HostBuilder
4. ✅ Inject `ILogger<T>` via DI in your services
5. ✅ Set appropriate minimum log levels for production

### ❌ DON'T

1. ❌ Add Serilog console sink (`.WriteTo.Console()`) - breaks MCP protocol
2. ❌ Forget to flush logs before application exit
3. ❌ Log sensitive data (passwords, tokens, etc.)
4. ❌ Use Debug-level logging in production without filtering

## Troubleshooting

### "My MCP client hangs or shows protocol errors"

Check your log file for unexpected console output. The `[Console]` prefix will show you what's leaking.

### "Logs aren't being written"

- Ensure log directory exists and is writable
- Check that `await Log.CloseAndFlushAsync()` is called
- Verify Serilog configuration has `.CreateLogger()` called

### "Out-of-order log messages"

This is rare but can happen with high-volume concurrent logging. The timestamps allow you to correlate messages. If it's a problem, all writes are already protected by locks in `SerilogTextWriter`.

## Example: Complete MCP Server Setup

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using SerilogFileWriter;

namespace MyMcpServer;

public class Program
{
    public static async Task Main(string[] args)
    {
        // One-line logging setup
        Log.Logger = McpLoggingExtensions.SetupMcpLogging(
            "logs/mcp-server-.log",
            minimumLevel: LogEventLevel.Information);

        try
        {
            Log.Information("Starting MCP server...");

            var host = Host.CreateDefaultBuilder(args)
                .UseSerilog() // Use Serilog for all ILogger<T> injection
                .ConfigureServices(services =>
                {
                    services.AddMcpServer(); // Your MCP server registration
                    services.AddTransient<MyService>();
                })
                .Build();

            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "MCP server terminated unexpectedly");
            throw;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }
}

public class MyService
{
    private readonly ILogger<MyService> _logger;

    public MyService(ILogger<MyService> logger)
    {
        _logger = logger;
    }

    public void DoWork()
    {
        // All these go to the same log file:
        _logger.LogInformation("Using ILogger<T> from DI");
        Console.WriteLine("Using Console.WriteLine");
        Log.Information("Using Serilog directly");
    }
}
```

## Dependencies

- `Serilog` (>= 4.3.1)
- `Serilog.Sinks.File` (>= 7.0.1)
- `Microsoft.Extensions.Hosting` (>= 10.0.0)
- `Microsoft.Extensions.Logging` (>= 10.0.0)

## License

Same as parent project.

## Related

See `ConsoleLoggingTest` project for comprehensive POC tests validating this approach.
