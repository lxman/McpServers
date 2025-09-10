namespace McpCodeEditor.Models.Angular;

/// <summary>
/// Angular dependency injection information
/// </summary>
public class AngularDependency
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? InjectionToken { get; set; }
    public bool IsOptional { get; set; }
    public bool IsSelf { get; set; }
    public bool IsSkipSelf { get; set; }
    public string ImportPath { get; set; } = string.Empty;
    public int Line { get; set; }
}
