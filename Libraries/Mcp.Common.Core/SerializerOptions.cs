using System.Text.Json;

namespace Mcp.Common.Core;

/// <summary>
/// Provides common JSON serializer options for MCP servers
/// </summary>
public static class SerializerOptions
{
    /// <summary>
    /// Gets JSON serializer options with indented formatting for human-readable output
    /// </summary>
    public static readonly JsonSerializerOptions JsonOptionsIndented = new() { WriteIndented = true };

    /// <summary>
    /// Gets JSON serializer options with compact formatting for minimal size
    /// </summary>
    public static readonly JsonSerializerOptions JsonOptionsCompact = new() { WriteIndented = false };
}
