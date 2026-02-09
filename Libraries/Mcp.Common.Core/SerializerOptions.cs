using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mcp.Common.Core;

/// <summary>
/// Provides common JSON serializer options for MCP servers.
/// All instances are static readonly — do not modify after initialization.
/// </summary>
public static class SerializerOptions
{
    /// <summary>
    /// Indented formatting for human-readable output.
    /// </summary>
    public static readonly JsonSerializerOptions JsonOptionsIndented = new() { WriteIndented = true };

    /// <summary>
    /// Compact formatting for minimal size.
    /// </summary>
    public static readonly JsonSerializerOptions JsonOptionsCompact = new() { WriteIndented = false };

    /// <summary>
    /// Indented with camelCase property names for API responses.
    /// </summary>
    public static readonly JsonSerializerOptions JsonOptionsCamelCase = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Handles complex objects with circular references and null suppression.
    /// </summary>
    public static readonly JsonSerializerOptions JsonOptionsComplex = new()
    {
        WriteIndented = true,
        MaxDepth = 32,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Permissive deserialization for config files with comments and trailing commas.
    /// </summary>
    public static readonly JsonSerializerOptions JsonOptionsPermissive = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}
