using System.ComponentModel;
using Mcp.Hosting.Core;
using ModelContextProtocol.Server;

namespace McpGateway.TestBackend;

[McpServerToolType]
public class EchoTools(McpHostOptions options)
{
    [McpServerTool, DisplayName("echo_version")]
    [Description("Returns the version this backend was started with, and the calling client id.")]
    public string EchoVersion() => $"{options.Version}|{McpCaller.ClientId}";
}
