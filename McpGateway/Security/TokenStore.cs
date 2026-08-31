using Mcp.Hosting.Core;

namespace McpGateway.Security;

public static class TokenStore
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// <summary>
    /// Reads the bearer token, generating one on first run. Loopback HTTP is reachable by every
    /// process on the machine, so this is the only thing stopping any of them from driving
    /// desktop-commander or ssh-mcp.
    /// </summary>
    public static string GetOrCreate(string path)
    {
        Gate.Wait();
        try
        {
            if (File.Exists(path))
            {
                string existing = File.ReadAllText(path).Trim();
                if (existing.Length > 0) return existing;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string token = BearerToken.Generate();

            string temp = path + ".tmp";
            File.WriteAllText(temp, token);
            File.Move(temp, path, overwrite: true);

            return token;
        }
        finally
        {
            Gate.Release();
        }
    }
}
