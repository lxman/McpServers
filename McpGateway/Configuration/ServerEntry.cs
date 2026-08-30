using System.Text.Json.Serialization;

namespace McpGateway.Configuration;

public sealed record ServerEntry
{
    [JsonPropertyName("project")] public required string Project { get; init; }
    [JsonPropertyName("assembly")] public required string Assembly { get; init; }
    [JsonPropertyName("deployRoot")] public required string DeployRoot { get; init; }
    [JsonPropertyName("activeVersion")] public required string ActiveVersion { get; init; }

    /// <summary>"shared" gives every caller one backend; "per-client" gives each its own.</summary>
    [JsonPropertyName("pool")] public string Pool { get; init; } = "per-client";

    /// <summary>False for servers whose machine-wide state two live instances would corrupt.</summary>
    [JsonPropertyName("overlapAllowed")] public bool OverlapAllowed { get; init; } = true;

    [JsonPropertyName("eagerStart")] public bool EagerStart { get; init; }
    [JsonPropertyName("idleTimeoutMinutes")] public int IdleTimeoutMinutes { get; init; } = 30;
    [JsonPropertyName("startupTimeoutSeconds")] public int StartupTimeoutSeconds { get; init; } = 30;

    [JsonIgnore]
    public bool IsShared => string.Equals(Pool, "shared", StringComparison.OrdinalIgnoreCase);
}
