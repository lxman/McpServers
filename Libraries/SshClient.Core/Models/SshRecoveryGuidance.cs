namespace SshClient.Core.Models;

/// <summary>
/// Guidance for agents when an SSH operation can be recovered with another tool call.
/// </summary>
public record SshRecoveryGuidance
{
    public required string Message { get; init; }

    public IReadOnlyList<string> Steps { get; init; } = [];

    public IReadOnlyList<string> Tools { get; init; } = [];

    public IReadOnlyList<string> AskUserWhenMissing { get; init; } = [];
}

public static class SshRecoveryCodes
{
    public const string ConnectionNotConnected = "SSH_CONNECTION_NOT_CONNECTED";
    public const string NoMatchingProfile = "SSH_NO_MATCHING_PROFILE";
}

