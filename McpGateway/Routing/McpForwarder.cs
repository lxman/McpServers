using System.Net;
using McpGateway.Configuration;
using McpGateway.Security;
using McpGateway.Supervision;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Forwarder;

namespace McpGateway.Routing;

public sealed class McpForwarder(
    IHttpForwarder forwarder,
    BackendSupervisor supervisor,
    ManifestStore manifest,
    BackendToken backendToken,
    ILogger<McpForwarder> logger)
{
    // Long timeout: a streamable-HTTP POST response can be a text/event-stream the handler holds
    // open, and YARP must not cut it short.
    private readonly HttpMessageInvoker _invoker = new(new SocketsHttpHandler
    {
        UseProxy = false,
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.None,
        ActivityHeadersPropagator = null,
        ConnectTimeout = TimeSpan.FromSeconds(15)
    });

    private static readonly ForwarderRequestConfig RequestConfig = new()
    {
        ActivityTimeout = TimeSpan.FromMinutes(10)
    };

    public async Task ForwardAsync(HttpContext context, string server, string suffix)
    {
        if (!manifest.TryGet(server, out ServerEntry? entry))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsync($"No server named '{server}'.");
            return;
        }

        var key = new BackendKey(server, ClientIdentity.ResolvePoolKey(context, entry!));

        BackendInstance instance;
        try
        {
            instance = await supervisor.GetOrStartAsync(key, context.RequestAborted);
        }
        catch (BackendStartupException ex)
        {
            logger.LogError(ex, "Could not start {Key}", key);

            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsync(
                ex.LogTail.Length == 0 ? ex.Message : $"{ex.Message}\n\n{ex.LogTail}");
            return;
        }

        using IDisposable lease = instance.BeginRequest();

        // Rewrite /{server}/mcp to /mcp — backends don't know they're behind a gateway.
        var transformer = new PathTransformer(suffix, backendToken.Value);

        ForwarderError error = await forwarder.SendAsync(
            context, instance.DestinationPrefix, _invoker, RequestConfig, transformer);

        if (error != ForwarderError.None)
        {
            logger.LogWarning("Forwarding to {Key} failed with {Error}", key, error);
        }
    }

    private sealed class PathTransformer(string suffix, string backendToken) : HttpTransformer
    {
        public override async ValueTask TransformRequestAsync(
            HttpContext context,
            HttpRequestMessage request,
            string destinationPrefix,
            CancellationToken cancellationToken)
        {
            await base.TransformRequestAsync(
                context, request, destinationPrefix, cancellationToken);

            // Keep the query string. base.TransformRequestAsync built it into RequestUri, and
            // overwriting the URI here would drop it silently rather than failing loudly.
            request.RequestUri = new Uri(
                destinationPrefix.TrimEnd('/') + suffix + context.Request.QueryString);

            // X-Mcp-Client must survive -- the backend's McpCaller.ClientId reads it.
            //
            // The caller's own token must not: it is the client-facing credential and a backend
            // has no business being able to replay it. Swap in the gateway's separate backend
            // token instead, which is what the backend actually authenticates against. Replacing
            // rather than removing is load-bearing now that /mcp is guarded: strip it and every
            // forwarded call comes back 401.
            request.Headers.Remove("Authorization");
            request.Headers.Add("Authorization", $"Bearer {backendToken}");

            request.Headers.Host = null;
        }
    }
}
