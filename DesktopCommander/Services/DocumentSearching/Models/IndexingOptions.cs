namespace DesktopCommander.Services.DocumentSearching.Models;

public class IndexingOptions
{
    public List<string> IncludePatterns { get; set; } = ["*"];
    public List<string> ExcludePatterns { get; set; } = ["**/temp/**", "**/.git/**", "**/bin/**", "**/obj/**"];
    public bool Recursive { get; set; } = true;
    public bool IncludeContent { get; set; } = true;
    public bool ExtractStructuredData { get; set; } = true;
    public bool GenerateSummaries { get; set; } = false;
    public bool DetectLanguages { get; set; } = false;
    public bool ExtractKeyTerms { get; set; } = true;
    public bool BuildRelationships { get; set; } = false;
    public int MaxFileSizeMB { get; set; } = 100;
    public int MaxConcurrency { get; set; } = Environment.ProcessorCount;
    public bool SkipPasswordProtected { get; set; } = false;
    public bool AutoDetectPasswords { get; set; } = true;
}