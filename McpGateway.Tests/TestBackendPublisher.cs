using System.Diagnostics;
using Xunit;

namespace McpGateway.Tests;

/// <summary>
/// Publishes McpGateway.TestBackend for the fixtures that need a real backend on disk.
/// <para>
/// ONE AT A TIME, and that is the whole point of this class existing. Two fixtures publish this
/// same csproj, xUnit runs their classes in parallel, and a distinct <c>-o</c> does not separate
/// them: MSBuild still builds through the project's OWN bin/obj before copying to the output. Two
/// concurrent publishes therefore race on files like
/// <c>bin/Debug/net10.0/McpGateway.TestBackend.runtimeconfig.json</c>, and the loser dies with
/// MSB4018 out of GenerateRuntimeConfigurationFiles -- inside a fixture's InitializeAsync, which
/// fails every test in that class at once.
/// </para>
/// <para>
/// That was the intermittent "ten failures, always BackendAuthTests, never reproducible" run: ten
/// because that class has ten tests, and intermittent because the window only opens when the
/// project actually has to build. With warm bin/obj the second publish is a no-op and the race
/// closes, which is why it survived several clean runs and vanished under a filter.
/// </para>
/// </summary>
internal static class TestBackendPublisher
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// <summary>
    /// Publishes the test backend to <paramref name="outputDirectory"/>. <paramref name="label"/>
    /// only names the publish in the failure message.
    /// </summary>
    public static void Publish(string outputDirectory, string label)
    {
        Gate.Wait();

        try
        {
            Run(outputDirectory, label);
        }
        finally
        {
            Gate.Release();
        }
    }

    private static void Run(string outputDirectory, string label)
    {
        var info = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (string arg in new[]
                 {
                     "publish", RepoPath("McpGateway.TestBackend/McpGateway.TestBackend.csproj"),
                     "-c", "Debug", "-o", outputDirectory, "--nologo", "-v", "quiet"
                 })
        {
            info.ArgumentList.Add(arg);
        }

        using Process process = Process.Start(info)!;

        // Both pipes have to be drained concurrently. Reading stderr to the end while stdout fills
        // its 4 KB buffer deadlocks the publish -- the child blocks on a write nobody is reading
        // and WaitForExit never returns. Seen for real on a publish whose dependency graph had
        // just been rebuilt, so this is not theoretical.
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        Assert.True(process.ExitCode == 0,
            $"publish {label} failed: {stderr.Result}{Environment.NewLine}{stdout.Result}");
    }

    private static string RepoPath(string relative) => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", relative));
}
