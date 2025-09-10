namespace McpCodeEditor.Models.Options;

public class SearchOptions
{
    public bool IncludeComments { get; set; } = true;
    public bool IncludeStrings { get; set; } = true;
    public bool CaseSensitive { get; set; } = false;
    public bool WholeWord { get; set; } = false;
    public bool UseRegex { get; set; } = false;
    public bool UseFuzzyMatch { get; set; } = false;
    public int FuzzyThreshold { get; set; } = 70;
    public int MaxResults { get; set; } = 100;
    public string[]? FileExtensions { get; set; }
    public string[]? ExcludePaths { get; set; }
}
