using DirectoryMcp;
using DirectoryMcp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Configure logging
builder.Logging.ClearProviders();
// builder.Logging.AddConsole();
// builder.Logging.SetMinimumLevel(LogLevel.Information);

// Add ServerRegistry as a singleton (loads servers.json on startup)
builder.Services.AddSingleton<ServerRegistry>();

// Configure HttpClient for making requests to other MCP servers
builder.Services.AddHttpClient("directory-client", client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Add("User-Agent", "DirectoryMcp/1.0");
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        // Allow self-signed certificates for localhost development
        ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
        {
            // Only bypass certificate validation for localhost
            if (message.RequestUri?.Host is "localhost" or "127.0.0.1")
            {
                return true;
            }
        
            // For all other hosts, use default validation
            return errors == System.Net.Security.SslPolicyErrors.None;
        }
    });

// Add MCP Server
builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<HttpTools>()
    .WithTools<ServerDirectory>();

IHost host = builder.Build();

await host.RunAsync();