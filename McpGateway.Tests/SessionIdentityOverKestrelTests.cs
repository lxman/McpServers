using System.Diagnostics;
using McpGateway.Configuration;
using McpGateway.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace McpGateway.Tests;

/// <summary>
/// The other identity tests build their HttpContext by hand, which cannot check the two things
/// per-session pooling actually rests on: that Kestrel's Connection.RemotePort and
/// Connection.LocalPort really are the client's port and ours, and that two separate client
/// processes resolve to two different keys. A hand-built DefaultHttpContext would agree with
/// whatever the implementation assumed.
/// </summary>
public sealed class SessionIdentityOverKestrelTests
{
    private static ServerEntry PerSession => new()
    {
        Project = "Demo/Demo.csproj",
        Assembly = "Demo.dll",
        DeployRoot = "deploy/demo",
        ActiveVersion = "v-one",
        Pool = "per-session"
    };

    [Fact]
    public async Task TwoClientProcesses_GetTwoDifferentKeys_OverRealKestrel()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        await using WebApplication app = builder.Build();
        app.MapGet("/key", (HttpContext context) => ClientIdentity.ResolvePoolKey(context, PerSession));

        await app.StartAsync(TestContext.Current.CancellationToken);

        try
        {
            int port = new Uri(app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!.Addresses.First()).Port;

            string url = $"http://127.0.0.1:{port}/key";

            using var http = new HttpClient();
            string mine = (await http.GetStringAsync(url, TestContext.Current.CancellationToken)).Trim();

            (int childPid, string theirs) = await CurlAsync(url, TestContext.Current.CancellationToken);

            // Each key must name the process that actually opened the socket -- this side the test
            // host, the other side curl.
            Assert.StartsWith($"s-{Environment.ProcessId}-", mine);
            Assert.StartsWith($"s-{childPid}-", theirs);
            Assert.NotEqual(mine, theirs);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// A second process to be the other session. curl is pinned to 127.0.0.1 and HTTP/1.1 on
    /// purpose: the table lookup queries AF_INET, so a client that quietly chose ::1 would resolve
    /// to nothing and the test would fail for a reason that has nothing to do with the code.
    /// </summary>
    private static async Task<(int Pid, string Body)> CurlAsync(
        string url, CancellationToken cancellationToken)
    {
        string curl = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System), "curl.exe");

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(curl, $"-s --http1.1 {url}")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        string body = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        return (process.Id, body.Trim());
    }
}
