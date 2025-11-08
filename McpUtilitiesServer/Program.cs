using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace McpUtilitiesServer;

public class Program
{
    public static async Task Main(string[] args)
    {
        await Console.Error.WriteLineAsync("McpUtilitiesServer - Starting MCP server...");

        // Create application builder with proper logging setup
        var builder = Host.CreateApplicationBuilder();

        // Configure all logs to go to stderr instead of stdout
        // This is important for stdio transport since stdout is reserved for MCP communication
        builder.Logging.AddConsole(consoleLogOptions =>
        {
            consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
        });

        // Configure the MCP server with proper tool registration
        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithTools<TimeUtilities>();

        var host = builder.Build();

        await Console.Error.WriteLineAsync("MCP time utilities server ready to handle requests");
        await host.RunAsync();
    }
}
