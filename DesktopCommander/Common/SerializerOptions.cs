using System.Text.Json;

namespace DesktopCommander.Common;

public static class SerializerOptions
{
    public static JsonSerializerOptions JsonOptionsIndented => new() { WriteIndented = true };
}