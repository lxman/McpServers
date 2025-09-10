namespace McpCodeEditor.Models.Options;

public class BatchReplaceOptions
{
    public string SearchPattern { get; set; } = string.Empty;
    public string ReplaceWith { get; set; } = string.Empty;
    public bool UseRegex { get; set; } = false;
    public bool CaseSensitive { get; set; } = false;
    public string FilePattern { get; set; } = "*";
    public List<string> ExcludeDirectories { get; set; } = [];
    public bool CreateBackup { get; set; } = true;
}
