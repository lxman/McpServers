namespace CodeAssist.Core.Services;

/// <summary>
/// The single source of truth for Qdrant collection names.
/// </summary>
/// <remarks>
/// This exists because there were two. <c>RepositoryIndexer</c> created collections as the sanitized
/// repository name ("pellucid"), while <c>L2PromotionService</c>, when no mapping had been registered,
/// guessed <c>codeassist_{folderName}</c>. The guess could never match a real collection, so promotion
/// looked up a collection that did not exist, logged a warning, and dropped the write — meaning an edit
/// made while a repository was watched-but-unregistered lived in the L1 cache until the process exited
/// and then vanished, with nothing surfaced to the caller.
///
/// <para>Two functions that must agree, written separately, will eventually disagree. Keep this the only
/// place a collection name is derived.</para>
/// </remarks>
public static class CollectionNaming
{
    /// <summary>
    /// Qdrant collection names must be alphanumeric with underscores, so every other character
    /// becomes one, and the result is lower-cased.
    /// </summary>
    public static string ForRepository(string repositoryName) =>
        new string(repositoryName.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray()).ToLowerInvariant();
}
