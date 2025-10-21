namespace PlaywrightServerMcp.Models;

/// <summary>
/// Component complexity metrics
/// </summary>
public class ComponentComplexity
{
    public int TemplateSize { get; set; }
    public int StyleSize { get; set; }
    public int LogicSize { get; set; }
    public int ImportCount { get; set; }
    public int MethodCount { get; set; }
    public int LifecycleHookCount { get; set; }
    public string ComplexityLevel { get; set; } = string.Empty; // simple, moderate, complex, highly-complex
}