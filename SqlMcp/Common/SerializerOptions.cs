using System.Text.Json;

namespace SqlMcp.Common;

public static class SerializerOptions
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static readonly JsonSerializerOptions JsonOptionsIndented = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };
}
