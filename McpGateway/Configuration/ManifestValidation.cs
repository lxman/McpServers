namespace McpGateway.Configuration;

/// <summary>
/// Manifest combinations that load cleanly and then hurt at runtime.
/// <para>
/// Reported, never refused. That follows the rule the job object already sets -- a degraded gateway
/// beats one that will not start -- and one bad entry must not take every other server down with
/// it.
/// </para>
/// </summary>
public static class ManifestValidation
{
    public static IReadOnlyList<string> Warnings(IReadOnlyDictionary<string, ServerEntry> entries)
    {
        var warnings = new List<string>();

        foreach ((string name, ServerEntry entry) in entries)
        {
            // per-session gives one backend per calling process. With no idle timeout none is ever
            // reaped, so the process count grows with every session that has ever connected and
            // only a gateway restart clears it -- and Stage 3 puts thirteen more servers on this
            // setting. Shared is exempt on purpose: its count is bounded at one, which is exactly
            // what code-assist runs today.
            if (entry.IsPerSession && entry.IdleTimeoutMinutes <= 0)
            {
                warnings.Add(
                    $"'{name}' is pool: per-session with idleTimeoutMinutes: " +
                    $"{entry.IdleTimeoutMinutes}, so it starts one backend per calling process and " +
                    "never reaps any of them. Set a nonzero idleTimeoutMinutes.");
            }
        }

        return warnings;
    }
}
