using McpGateway.Security;
using Xunit;

namespace McpGateway.Tests;

public sealed class TokenStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "mcp-token-" + Guid.NewGuid().ToString("N"));

    private string TokenPath => Path.Combine(_directory, "token");

    [Fact]
    public void GetOrCreate_GeneratesOnFirstCall()
    {
        string token = TokenStore.GetOrCreate(TokenPath);

        Assert.True(File.Exists(TokenPath));
        Assert.True(token.Length >= 40, $"token was only {token.Length} chars");
    }

    [Fact]
    public void GetOrCreate_IsStableAcrossCalls()
    {
        string first = TokenStore.GetOrCreate(TokenPath);
        string second = TokenStore.GetOrCreate(TokenPath);

        Assert.Equal(first, second);
    }

    [Fact]
    public void GetOrCreate_GeneratesDistinctTokensForDistinctPaths()
    {
        string first = TokenStore.GetOrCreate(TokenPath);
        string second = TokenStore.GetOrCreate(Path.Combine(_directory, "other"));

        Assert.NotEqual(first, second);
    }

    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); }
        catch (DirectoryNotFoundException) { }
    }
}
