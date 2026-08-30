using Xunit;

namespace Mcp.Hosting.Core.Tests;

public sealed class PortFileTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "mcp-portfile-" + Guid.NewGuid().ToString("N"));

    private string TargetPath => Path.Combine(_directory, "port.json");

    public PortFileTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task WriteThenRead_RoundTrips()
    {
        await PortFile.WriteAsync(TargetPath, 51234, 4242, TestContext.Current.CancellationToken);

        Assert.True(PortFile.TryRead(TargetPath, out PortFileContent content));
        Assert.Equal(51234, content.Port);
        Assert.Equal(4242, content.Pid);
    }

    [Fact]
    public void TryRead_ReturnsFalse_WhenMissing()
    {
        Assert.False(PortFile.TryRead(TargetPath, out _));
    }

    [Fact]
    public void TryRead_ReturnsFalse_WhenPartial()
    {
        File.WriteAllText(TargetPath, "{\"Port\":512");

        Assert.False(PortFile.TryRead(TargetPath, out _));
    }

    [Fact]
    public void TryRead_ReturnsFalse_WhenPortIsZero()
    {
        File.WriteAllText(TargetPath, "{\"Port\":0,\"Pid\":1,\"StartedAt\":\"2026-08-30T00:00:00+00:00\"}");

        Assert.False(PortFile.TryRead(TargetPath, out _));
    }

    [Fact]
    public async Task WriteAsync_LeavesNoTempFileBehind()
    {
        await PortFile.WriteAsync(TargetPath, 51234, 4242, TestContext.Current.CancellationToken);

        string[] files = Directory.GetFiles(_directory);
        Assert.Equal(new[] { TargetPath }, files);
    }

    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); }
        catch (DirectoryNotFoundException) { }
    }
}
