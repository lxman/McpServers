using System.Text.Json;

namespace CSharpAnalyzer.Models;

public static class SerializerOptions
{
    public static JsonSerializerOptions JsonOptionsIndented => new() { WriteIndented = true };
}