namespace McpGateway.Upgrade;

public sealed record ActivationResult(
    bool Succeeded,
    string Server,
    string FromVersion,
    string ToVersion,
    int BackendsSwapped,
    bool DrainTimedOut,
    string? Error);
