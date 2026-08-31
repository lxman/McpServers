using McpGateway.Configuration;
using Xunit;

namespace McpGateway.Tests;

/// <summary>
/// The manifest accepts combinations that are individually sensible and jointly harmful. These are
/// warnings rather than refusals, deliberately: the house rule elsewhere in the gateway is that a
/// degraded gateway beats one that will not start.
/// </summary>
public sealed class ManifestValidationTests
{
    private static ServerEntry Entry(string pool, int idleTimeoutMinutes) => new()
    {
        Project = "Demo/Demo.csproj",
        Assembly = "Demo.dll",
        DeployRoot = "deploy/demo",
        Pool = pool,
        IdleTimeoutMinutes = idleTimeoutMinutes
    };

    /// <summary>
    /// One backend per calling process, none of them ever reaped. Every session that ever connects
    /// leaves a process behind for as long as the gateway lives.
    /// </summary>
    [Fact]
    public void PerSessionWithNoIdleTimeout_IsReported()
    {
        var entries = new Dictionary<string, ServerEntry> { ["ssh"] = Entry("per-session", 0) };

        string[] warnings = ManifestValidation.Warnings(entries).ToArray();

        Assert.Single(warnings);
        Assert.Contains("ssh", warnings[0]);
    }

    [Fact]
    public void PerSessionWithAnIdleTimeout_IsNotReported()
    {
        var entries = new Dictionary<string, ServerEntry> { ["ssh"] = Entry("per-session", 30) };

        Assert.Empty(ManifestValidation.Warnings(entries));
    }

    /// <summary>
    /// code-assist is exactly this today, on purpose: one shared backend that is never reaped. The
    /// count is bounded at one, so it is not the hazard and must not be reported as one.
    /// </summary>
    [Fact]
    public void SharedWithNoIdleTimeout_IsNotReported()
    {
        var entries = new Dictionary<string, ServerEntry> { ["code-assist"] = Entry("shared", 0) };

        Assert.Empty(ManifestValidation.Warnings(entries));
    }
}
