using System.Text.Json;

namespace PlaywrightServerMcp.Common;

public static class SerializerOptions
{
    public static JsonSerializerOptions JsonOptionsIndented => new() { WriteIndented = true };
}