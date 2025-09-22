using System.ComponentModel;
using ModelContextProtocol.Server;

namespace AzureMcp.Prompts;

/// <summary>
/// Empty prompt provider to satisfy MCP protocol initialization requirements.
/// Returns truly empty prompt list to prevent "server failed to initialize" toast messages.
/// </summary>
[McpServerPromptType]
public class EmptyPromptProvider
{
    /// <summary>
    /// Required empty constructor for MCP framework
    /// </summary>
    public EmptyPromptProvider()
    {
    }

    /// <summary>
    /// Returns an empty list of prompts to satisfy MCP capability enumeration.
    /// This prevents Claude from showing initialization failure toasts.
    /// </summary>
    /// <returns>Empty prompt collection</returns>
    [McpServerPrompt(Name = "empty")]
    [Description("Empty prompt collection - prevents MCP initialization warnings")]
    public static async Task<string> GetEmptyPromptAsync()
    {
        await Task.CompletedTask;
        return "Empty prompts list";
    }
}
