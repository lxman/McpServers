using McpCodeEditor.ServiceModules;
using Microsoft.Build.Locator;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace McpCodeEditor;

public class Program
{
    public static async Task Main(string[] args)
    {
        if (!MSBuildLocator.IsRegistered)
        {
            MSBuildLocator.RegisterDefaults();
        }
        
        // Configure Serilog FIRST - before any other logging
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .WriteTo.File(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Claude", "logs", "mcp-code-editor-debug.log"),
                shared:true,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("=== MCP Code Editor Server Starting ===");
        Log.Information("Arguments: {@Args}", args);

        try
        {
            // CRITICAL: Redirect all console output to prevent JSON-RPC corruption
            // Various components can write warnings to stdout
            Console.SetOut(TextWriter.Null);
            Console.SetError(TextWriter.Null);

            Log.Information("Console output redirected");

            // Create the host builder with configuration
            HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

            // Add configuration sources
            // CRITICAL: Use an absolute path to appsettings.json since the working directory differs in MCP
            string configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            Log.Information("Loading configuration from: {ConfigPath}", configPath);

            builder.Configuration
                .AddJsonFile(configPath, optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .AddCommandLine(args);

            Log.Information("Configuration loaded successfully");

            // Configure services using modular approach
            Log.Information("Starting modular service registration...");

            builder.Services
                // Add memory caching for performance
                .AddMemoryCache()
                
                // PHASE 1 REFACTORING: Use service modules instead of massive inline registration
                .AddCoreServices()                  // Core application services
                .AddArchitectureServices()          // Architecture analysis services
                .AddTypeScriptServices()           // TypeScript analysis services
                .AddRefactoringServices()          // All refactoring services
                .AddSpecializedServices()          // Code generation, batch ops, search, etc.
                
                // PHASE 3 REFACTORING: Strategy Pattern for language-specific operations
                .AddStrategies()                   // Language refactoring strategies
                
                // PHASE 3 TASK 2: Command Pattern for refactoring operations
                .AddCommands()                     // Refactoring commands
                
                // CRITICAL: Use Serilog instead of default logging
                .AddLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddSerilog(Log.Logger);
                });

            // Configure MCP Server with stdio transport and modular tools
            Log.Information("Configuring MCP server and tools...");
            
            builder.Services
                .AddMcpServer()
                .WithStdioServerTransport()
                .AddAllTools();  // PHASE 1 REFACTORING: Use tools module

            Log.Information("Modular service registration completed successfully");

            // Build and run the host
            Log.Information("Building host...");
            IHost host = builder.Build();
            Log.Information("Host built successfully");

            // Start the MCP server
            Log.Information("Starting MCP server...");
            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            throw;
        }
        finally
        {
            Log.Information("=== MCP Code Editor Server Shutting Down ===");
            await Log.CloseAndFlushAsync();
        }
    }
}
