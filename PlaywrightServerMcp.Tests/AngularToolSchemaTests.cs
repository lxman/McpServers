using System.Text.Json;
using ModelContextProtocol.Server;
using Playwright.Core.Services;
using PlaywrightServerMcp.Tools;
using Xunit;

namespace PlaywrightServerMcp.Tests;

/// <summary>
/// A runtime refusal only tells a client it was wrong after it has already called. The generated
/// tool schema should say up front that workingDirectory has to be supplied, so an omitted one is
/// caught before the call is made.
/// </summary>
public class AngularToolSchemaTests
{
    private readonly PlaywrightSessionManager _sessions = new();

    public static IEnumerable<object[]> AngularToolsTakingAWorkingDirectory()
    {
        var cli = new AngularCliIntegration(new PlaywrightSessionManager());
        var bundles = new AngularBundleAnalyzer(new PlaywrightSessionManager());
        var config = new AngularConfigurationAnalyzer(new PlaywrightSessionManager());
        var testing = new AngularTestingIntegration(new PlaywrightSessionManager());

        yield return [(Delegate)cli.ExecuteNgCommands];
        yield return [(Delegate)cli.CheckAngularCliStatus];
        yield return [(Delegate)cli.GenerateAngularArtifact];
        yield return [(Delegate)cli.BuildAngularProject];
        yield return [(Delegate)bundles.AnalyzeBundleSizeByComponent];
        yield return [(Delegate)config.AnalyzeAngularJsonConfig];
        yield return [(Delegate)testing.ExecuteAngularUnitTests];
    }

    [Theory]
    [MemberData(nameof(AngularToolsTakingAWorkingDirectory))]
    public void The_schema_marks_workingDirectory_required(Delegate tool)
    {
        McpServerTool created = McpServerTool.Create(tool);
        JsonElement schema = created.ProtocolTool.InputSchema;

        Assert.True(
            schema.TryGetProperty("required", out JsonElement required),
            $"{created.ProtocolTool.Name} declares no required parameters: {schema}");

        Assert.Contains(
            "workingDirectory",
            required.EnumerateArray().Select(element => element.GetString()));
    }
}
