using System.Text.Json;

namespace AzureServer.Common;

public static class SerializerOptions
{
    public static JsonSerializerOptions JsonOptionsIndented => new() { WriteIndented = true };
}