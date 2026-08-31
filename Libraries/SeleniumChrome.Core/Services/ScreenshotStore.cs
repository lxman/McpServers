namespace SeleniumChrome.Core.Services;

/// <summary>
/// Where screenshots are written.
/// <para>
/// This exists because both call sites used a bare relative "Screenshots" directory, which resolved
/// against the process working directory. Under the MCP gateway that directory is a VERSIONED
/// deploy path, so screenshots would have landed under one version and the next deploy would have
/// started writing somewhere else, orphaning the earlier ones. Same class of bug as edgar's
/// "./data".
/// </para>
/// <para>
/// The default is absolute and per-user so the library is correct on its own. The host overrides
/// <see cref="Root"/> at startup to keep this server's files beside every other MCP server's.
/// </para>
/// </summary>
public static class ScreenshotStore
{
    /// <summary>Absolute. Set once at composition; read on every screenshot.</summary>
    public static string Root { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SeleniumChrome", "Screenshots");

    /// <summary>
    /// Full path for a screenshot file, with the directory created. Creating it here also fixes a
    /// real bug: the debug-screenshot path never created the directory at all and relied on some
    /// other code path having done it first, so it failed whenever it ran on its own.
    /// </summary>
    public static string PathFor(string fileName)
    {
        Directory.CreateDirectory(Root);
        return Path.Combine(Root, fileName);
    }
}
