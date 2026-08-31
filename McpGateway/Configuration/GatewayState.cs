using System.Text.Json.Serialization;

namespace McpGateway.Configuration;

/// <summary>
/// Everything the gateway learns at runtime, kept out of the git-tracked servers.json.
/// <para>
/// Active versions used to live in servers.json, which the gateway rewrote on every activation.
/// That left the working tree dirty after each deploy, and a git checkout, stash or pull silently
/// reverted the field -- surfacing much later, at the next gateway start, as a deploy directory
/// named after whatever the committed value happened to be. The repo is worked in daily, so that
/// was not a theoretical hazard.
/// </para>
/// </summary>
public sealed record GatewayState
{
    /// <summary>Server name to the version currently deployed for it.</summary>
    [JsonPropertyName("activeVersions")]
    public Dictionary<string, string> ActiveVersions { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);
}
