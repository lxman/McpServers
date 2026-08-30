using System.Text.Json;

namespace Mcp.Hosting.Core;

/// <summary>How a backend tells the gateway which port the OS gave it.</summary>
public sealed record PortFileContent(int Port, int Pid, DateTimeOffset StartedAt);

public static class PortFile
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Write via a temp file and a move. The gateway polls this path, so it must never observe a
    /// half-written file.
    /// </summary>
    public static async Task WriteAsync(
        string path, int port, int pid, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var content = new PortFileContent(port, pid, DateTimeOffset.UtcNow);
        string temp = path + ".tmp";

        await File.WriteAllTextAsync(
            temp, JsonSerializer.Serialize(content, Options), cancellationToken);

        File.Move(temp, path, overwrite: true);
    }

    public static bool TryRead(string path, out PortFileContent content)
    {
        content = null!;

        try
        {
            if (!File.Exists(path)) return false;

            PortFileContent? parsed = JsonSerializer.Deserialize<PortFileContent>(
                File.ReadAllText(path), Options);

            if (parsed is null || parsed.Port <= 0) return false;

            content = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
