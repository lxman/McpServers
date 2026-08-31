using Xunit;

namespace Mcp.Hosting.Core.Tests;

/// <summary>
/// A converted server runs from a VERSIONED deploy directory, so any relative data path it had
/// under stdio silently moves on every deploy and takes the user's data with it. These pin the
/// resolution that stops that.
/// </summary>
public sealed class DataPathTests
{
    private static string Root(string server) => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "McpServers", "data", server);

    [Fact]
    public void DataPathFor_IsAbsolute_AndPerServer()
    {
        string path = McpHttpHost.DataPathFor("edgar");

        Assert.True(Path.IsPathRooted(path));
        Assert.Equal(Root("edgar"), path);
        Assert.NotEqual(McpHttpHost.DataPathFor("document"), path);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveDataDirectory_FallsBackToTheServerRoot_WhenUnset(string? configured)
    {
        Assert.Equal(Root("edgar"), McpHttpHost.ResolveDataDirectory(configured, "edgar"));
    }

    [Fact]
    public void ResolveDataDirectory_KeepsAnAbsolutePath()
    {
        string configured = Path.Combine(Path.GetTempPath(), "edgar-data");

        Assert.Equal(configured, McpHttpHost.ResolveDataDirectory(configured, "edgar"));
    }

    /// <summary>
    /// The case that matters: "./data" is what edgar shipped with. Left alone it resolves against
    /// the process working directory -- which is the versioned deploy directory now, so a deploy
    /// would orphan everything saved under the previous version.
    /// </summary>
    [Theory]
    [InlineData("./data")]
    [InlineData("data")]
    [InlineData(@".\data")]
    public void ResolveDataDirectory_AnchorsARelativePathToTheServerRoot(string configured)
    {
        string resolved = McpHttpHost.ResolveDataDirectory(configured, "edgar");

        Assert.True(Path.IsPathRooted(resolved));
        Assert.Equal(Path.Combine(Root("edgar"), "data"), resolved);
        Assert.DoesNotContain(Directory.GetCurrentDirectory(), resolved);
    }
}
