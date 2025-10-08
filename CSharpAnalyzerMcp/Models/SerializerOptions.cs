using System.Text.Json;

namespace CSharpAnalyzerMcp.Models;

public static class SerializerOptions
{
    public static JsonSerializerOptions JsonOptionsIndented => new() { WriteIndented = true };
}