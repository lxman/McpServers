using System.Text.Json;
using McpGateway.Configuration;

namespace McpGateway.Tests;

/// <summary>
/// Writes the runtime state file the gateway merges active versions from. Active versions no longer
/// live in servers.json, so a fixture that wants a deployed server has to record one here.
/// </summary>
internal static class TestState
{
    /// <summary>Writes state.json under <paramref name="root"/> and returns its path.</summary>
    public static string Write(string root, params (string Server, string Version)[] active)
    {
        Directory.CreateDirectory(root);

        string path = Path.Combine(root, "state.json");

        var state = new GatewayState
        {
            ActiveVersions = active.ToDictionary(
                pair => pair.Server, pair => pair.Version, StringComparer.OrdinalIgnoreCase)
        };

        File.WriteAllText(path, JsonSerializer.Serialize(state));

        return path;
    }
}
