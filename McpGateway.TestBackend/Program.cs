using Mcp.Hosting.Core;
using McpGateway.TestBackend;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.AspNetCore;

WebApplicationBuilder builder = McpHttpHost.CreateBuilder(args, "test-backend");

builder.Services.AddMcpServer()
    .WithHttpTransport(o => o.SessionMode = HttpServerSessionMode.StatefulForInitializeClients)
    .WithTools<EchoTools>();

WebApplication app = builder.Build();
app.MapMcpHost();

await app.RunAsync();
