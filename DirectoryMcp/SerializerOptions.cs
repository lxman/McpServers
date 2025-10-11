using System.Text.Json;

namespace DirectoryMcp;

public static class SerializerOptions
{
    public static JsonSerializerOptions JsonOptionsIndented => new() { WriteIndented = true };
    
    public static JsonSerializerOptions JsonOptionsCaseInsensitive => new() { PropertyNameCaseInsensitive = true };
}