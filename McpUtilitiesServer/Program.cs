using Mcp.Hosting.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.AspNetCore;
using Serilog;

namespace McpUtilitiesServer;

public class Program
{
    public static async Task Main(string[] args)
    {
        try
        {
            // Logging, the loopback listener and the gateway's port-file contract all live in
            // McpHttpHost. Log.Logger is configured by the time this returns, so the old
            // SetupMcpLogging call and its relative "logs/" path are gone -- that path resolved
            // against the working directory, which is now a versioned deploy directory.
            WebApplicationBuilder builder = McpHttpHost.CreateBuilder(args, "time-utility");

            Log.Information("Starting Utilities server.");

            builder.Services
                .AddMcpServer()
                .WithHttpTransport(o => o.SessionMode = HttpServerSessionMode.StatefulForInitializeClients)
                .WithTools<TimeUtilities>();

            WebApplication app = builder.Build();
            app.MapMcpHost();

            // The "ready to handle requests" line on stderr is gone with the stdio transport. The
            // gateway does not read stderr; it waits for the port file McpHttpHost writes.
            await app.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Utilities server terminated unexpectedly");
            Environment.ExitCode = 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }
}
