namespace CodeAssist.Core.Analysis;

/// <summary>
/// Registry that maps languages to their semantic analyzers.
/// The indexing pipeline queries this per file to determine whether
/// a Tier 2 (semantic) pass is available for that language.
/// </summary>
public sealed class SemanticAnalyzerRegistry
{
    private readonly Dictionary<string, ISemanticAnalyzer> _analyzers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Register an analyzer for all languages it supports.
    /// </summary>
    public void Register(ISemanticAnalyzer analyzer)
    {
        foreach (string lang in analyzer.SupportedLanguages)
            _analyzers[lang] = analyzer;
    }

    /// <summary>
    /// Get the analyzer for a language, or null if only tree-sitter is available.
    /// </summary>
    public ISemanticAnalyzer? GetAnalyzer(string language)
    {
        return _analyzers.GetValueOrDefault(language);
    }

    /// <summary>
    /// Get all registered analyzers (deduplicated).
    /// </summary>
    public IEnumerable<ISemanticAnalyzer> GetAllAnalyzers()
    {
        return _analyzers.Values.Distinct();
    }
}
