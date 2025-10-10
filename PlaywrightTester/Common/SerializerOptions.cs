using System.Text.Json;

namespace PlaywrightTester.Common;

public static class SerializerOptions
{
    public static JsonSerializerOptions JsonOptionsIndented => new() { WriteIndented = true };
}