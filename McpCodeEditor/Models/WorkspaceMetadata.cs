using System.Text.Json.Serialization;

namespace McpCodeEditor.Models;

/// <summary>
/// Represents metadata for a workspace, including path mappings and access tracking
/// </summary>
public class WorkspaceMetadata
{
    /// <summary>
    /// 16-character hash identifier for the workspace
    /// </summary>
    [JsonPropertyName("hash")]
    public string Hash { get; set; } = string.Empty;

    /// <summary>
    /// Current full path to the workspace directory
    /// </summary>
    [JsonPropertyName("current_path")]
    public string CurrentPath { get; set; } = string.Empty;

    /// <summary>
    /// Original path when first detected (for tracking moves)
    /// </summary>
    [JsonPropertyName("original_path")]
    public string OriginalPath { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the workspace (usually project or directory name)
    /// </summary>
    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Last time this workspace was accessed/used
    /// </summary>
    [JsonPropertyName("last_accessed")]
    public DateTime LastAccessed { get; set; }

    /// <summary>
    /// When this workspace metadata was first created
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Number of times this workspace has been accessed
    /// </summary>
    [JsonPropertyName("access_count")]
    public int AccessCount { get; set; }

    /// <summary>
    /// Additional tags or metadata for categorization
    /// </summary>
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// Notes or description for the workspace
    /// </summary>
    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;
}
