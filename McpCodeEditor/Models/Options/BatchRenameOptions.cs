namespace McpCodeEditor.Models.Options;

public class BatchRenameOptions
{
    public string SearchPattern { get; set; } = string.Empty;
    public string ReplacePattern { get; set; } = string.Empty;
    public bool UseRegex { get; set; } = false;
    public bool RenameDirectories { get; set; } = false;
    public bool CreateBackup { get; set; } = true;
}
