namespace CodeAssist.Core.Services;

/// <summary>
/// The single source of truth for the form of a <c>relative_path</c> payload value.
/// </summary>
/// <remarks>
/// This exists because there were two forms. <see cref="RepositoryIndexer"/> discovers files through
/// <c>Microsoft.Extensions.FileSystemGlobbing</c>, which yields forward slashes on every platform, while
/// <c>HotCache</c> computes its path with <see cref="Path.GetRelativePath"/>, which yields backslashes on
/// Windows. The same file was therefore stored under two distinct keys, and a delete issued in one form
/// could not match rows written in the other — measured against a live collection, the backslash form of a
/// real file matched zero points while the forward-slash form matched thirty-two.
///
/// <para>Forward slashes are the target because existing rows already use them, the globbing library
/// produces them unprompted, and they are stable across platforms. Casing is deliberately preserved:
/// it is significant on Linux and Qdrant keyword matching is case-sensitive.</para>
///
/// <para>Normalize at the point a relative path is constructed, not at the call sites that consume it.
/// Two functions that must agree, applied separately, will eventually disagree.</para>
/// </remarks>
public static class IndexPath
{
    /// <summary>
    /// Convert a relative path to its canonical index form: forward slashes, no leading separator,
    /// no leading "./" segment.
    /// </summary>
    public static string Normalize(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return relativePath;

        string normalized = relativePath.Replace('\\', '/');

        if (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        return normalized.TrimStart('/');
    }
}
