using McpCodeEditor.Models.Refactoring;

namespace McpCodeEditor.Strategies;

/// <summary>
/// Factory interface for resolving language-specific refactoring strategies
/// </summary>
public interface ILanguageRefactoringStrategyFactory
{
    /// <summary>
    /// Get the appropriate refactoring strategy for the specified language
    /// </summary>
    /// <param name="language">The language type</param>
    /// <returns>The refactoring strategy for the language, or null if not supported</returns>
    ILanguageRefactoringStrategy? GetStrategy(LanguageType language);

    /// <summary>
    /// Get all available refactoring strategies
    /// </summary>
    /// <returns>All registered language refactoring strategies</returns>
    IEnumerable<ILanguageRefactoringStrategy> GetAllStrategies();
}

/// <summary>
/// Factory implementation for resolving language-specific refactoring strategies
/// Part of Phase 3 refactoring - Strategy Pattern implementation
/// </summary>
public class LanguageRefactoringStrategyFactory : ILanguageRefactoringStrategyFactory
{
    private readonly IEnumerable<ILanguageRefactoringStrategy> _strategies;

    public LanguageRefactoringStrategyFactory(IEnumerable<ILanguageRefactoringStrategy> strategies)
    {
        _strategies = strategies ?? throw new ArgumentNullException(nameof(strategies));
    }

    public ILanguageRefactoringStrategy? GetStrategy(LanguageType language)
    {
        return language switch
        {
            LanguageType.CSharp => _strategies.FirstOrDefault(s => s.Language == LanguageType.CSharp),
            LanguageType.TypeScript or LanguageType.JavaScript => _strategies.FirstOrDefault(s => s.Language == LanguageType.TypeScript),
            _ => null
        };
    }

    public IEnumerable<ILanguageRefactoringStrategy> GetAllStrategies()
    {
        return _strategies;
    }
}
