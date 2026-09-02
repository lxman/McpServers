using System.Text.Json;
using Xunit;
using Playwright.Core.Services;
using PlaywrightServerMcp.Tools;

namespace PlaywrightServerMcp.Tests;

/// <summary>
/// Every Angular tool that takes a <c>workingDirectory</c> must refuse a blank one rather than
/// quietly running against the server's own deploy directory. These call the tools for real -- the
/// refusal has to happen before any session lookup or process launch, so no browser and no <c>ng</c>
/// is involved.
/// </summary>
public class AngularToolWorkingDirectoryTests
{
    private readonly PlaywrightSessionManager _sessions = new();

    [Fact]
    public async Task ExecuteNgCommands_refuses_a_blank_working_directory()
        => AssertRefused(await new AngularCliIntegration(_sessions)
            .ExecuteNgCommands(command: "ng version", workingDirectory: ""));

    [Fact]
    public async Task CheckAngularCliStatus_refuses_a_blank_working_directory()
        => AssertRefused(await new AngularCliIntegration(_sessions)
            .CheckAngularCliStatus(workingDirectory: ""));

    [Fact]
    public async Task GenerateAngularArtifact_refuses_a_blank_working_directory()
        => AssertRefused(await new AngularCliIntegration(_sessions)
            .GenerateAngularArtifact(artifactType: "component", artifactName: "widget", workingDirectory: ""));

    [Fact]
    public async Task BuildAngularProject_refuses_a_blank_working_directory()
        => AssertRefused(await new AngularCliIntegration(_sessions)
            .BuildAngularProject(workingDirectory: ""));

    [Fact]
    public async Task AnalyzeBundleSizeByComponent_refuses_a_blank_working_directory()
        => AssertRefused(await new AngularBundleAnalyzer(_sessions)
            .AnalyzeBundleSizeByComponent(workingDirectory: ""));

    [Fact]
    public async Task AnalyzeAngularJsonConfig_refuses_a_blank_working_directory()
        => AssertRefused(await new AngularConfigurationAnalyzer(_sessions)
            .AnalyzeAngularJsonConfig(workingDirectory: ""));

    [Fact]
    public async Task ExecuteAngularUnitTests_refuses_a_blank_working_directory()
        => AssertRefused(await new AngularTestingIntegration(_sessions)
            .ExecuteAngularUnitTests(workingDirectory: ""));

    [Fact]
    public async Task A_relative_working_directory_is_refused_too()
        => AssertRefused(await new AngularConfigurationAnalyzer(_sessions)
            .AnalyzeAngularJsonConfig(workingDirectory: "./my-app"));

    private static void AssertRefused(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        Assert.True(
            root.TryGetProperty("ErrorMessage", out JsonElement error),
            $"Expected an ErrorMessage in the tool's response, got: {json}");

        string message = error.GetString() ?? "";
        Assert.Contains("workingDirectory", message);

        if (root.TryGetProperty("Success", out JsonElement success))
        {
            Assert.False(success.GetBoolean());
        }
    }
}
