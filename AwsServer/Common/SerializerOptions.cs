using System.Text.Json;

namespace AwsServer.Common;

public static class SerializerOptions
{
    public static JsonSerializerOptions JsonOptionsIndented => new() { WriteIndented = true };
}