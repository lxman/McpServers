using CSharpAnalyzerMcp.Services;
using CSharpAnalyzerMcp.Services.Reflection;
using CSharpAnalyzerMcp.Services.Roslyn;
using CSharpAnalyzerMcp.Tools.Reflection;
using CSharpAnalyzerMcp.Tools.Roslyn;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CSharpAnalyzerMcp;

public class Program
{
    public static async Task Main(string[] args)
    {
        await Console.Error.WriteLineAsync("CSharpAnalyzerMcp - Starting MCP server...");

        // Create application builder with proper logging setup
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        // Configure all logs to go to stderr instead of stdout
        // This is important for stdio transport since stdout is reserved for MCP communication
        builder.Logging.AddConsole(consoleLogOptions =>
        {
            consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
        });

        // Register Roslyn analysis service
        builder.Services.AddSingleton<RoslynAnalysisService>();

        // Register Reflection services
        builder.Services.AddSingleton<AssemblyLoaderService>();
        builder.Services.AddSingleton<AssemblyAnalysisService>();

        // Configure the MCP server with proper tool registration
        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithTools<RoslynTools>()
            .WithTools<ReflectionTools>();

        IHost host = builder.Build();

        await Console.Error.WriteLineAsync("MCP C# analyzer server ready to handle requests");
        await host.RunAsync();
    }
}
