namespace McpCodeEditor.Models;

public class BackupInfo
{
    public string Id { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string BackupPath { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public long SizeBytes { get; set; }
    public List<string> Files { get; set; } = [];
    
    // New workspace-specific metadata for %APPDATA% architecture
    public string WorkspaceHash { get; set; } = string.Empty;
    public string WorkspacePath { get; set; } = string.Empty;
    public string WorkspaceDisplayName { get; set; } = string.Empty;
}
