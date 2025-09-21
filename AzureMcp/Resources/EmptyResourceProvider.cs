using System.ComponentModel;
using ModelContextProtocol.Server;

namespace AzureMcp.Resources;

/// <summary>
/// Empty resource provider to satisfy MCP protocol initialization requirements.
/// Returns truly empty resource list to prevent "server failed to initialize" toast messages.
/// </summary>
[McpServerResourceType]
public class EmptyResourceProvider
{
    /// <summary>
    /// Required empty constructor for MCP framework
    /// </summary>
    public EmptyResourceProvider()
    {
    }

    /// <summary>
    /// Returns an empty list of resources to satisfy MCP capability enumeration.
    /// This prevents Claude from showing initialization failure toasts.
    /// </summary>
    /// <returns>Empty resource collection</returns>
    [McpServerResource(Name = "empty")]
    [Description("Empty resource collection - prevents MCP initialization warnings")]
    public async Task<string> GetEmptyResourceAsync()
    {
        await Task.CompletedTask;
        return "Empty resources list";
    }
}
