using System.Text.Json;

namespace McpUtilitiesServer;

public static class SerializerOptions
{
    public static JsonSerializerOptions JsonOptionsIndented => new() { WriteIndented = true };
}