using System.Text.Json;

namespace MongoTools.Common;

public static class SerializerOptions
{
    public static JsonSerializerOptions JsonOptionsIndented => new() { WriteIndented = true };
}