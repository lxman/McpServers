using System.Text.Json;

namespace AzureMcp.Common;

public static class SerializerOptions
{
    public static JsonSerializerOptions JsonOptionsIndented => new JsonSerializerOptions { WriteIndented = true };
}