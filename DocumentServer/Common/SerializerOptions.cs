using System.Text.Json;

namespace DocumentServer.Common;

public static class SerializerOptions
{
    private static readonly JsonSerializerOptions CaseInsensitiveTrue = new() { PropertyNameCaseInsensitive = true };
    private static readonly JsonSerializerOptions IndentedTrue = new() { WriteIndented = true };
    
    public static JsonSerializerOptions JsonOptionsIndented { get; } = IndentedTrue;
    public static JsonSerializerOptions JsonOptionsCaseInsensitiveTrue { get; } = CaseInsensitiveTrue;
}