using System.Text.Json;

namespace DesktopCommander.Core.Common;

public static class SerializerOptions
{
    public static JsonSerializerOptions JsonOptionsIndented => new() { WriteIndented = true };
    public static JsonSerializerOptions JsonOptionsCaseInsensitiveTrue => new() { PropertyNameCaseInsensitive = true };
}