namespace McpCodeEditor.Models.Angular;

/// <summary>
/// Angular component property (inputs, outputs, ViewChild, etc.)
/// </summary>
public class AngularProperty
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Decorator { get; set; } // @Input, @Output, @ViewChild, etc.
    public string? DecoratorParams { get; set; } // Parameters passed to decorator
    public bool IsOptional { get; set; }
    public string? DefaultValue { get; set; }
    public int Line { get; set; }
    public string? Description { get; set; } // From JSDoc comments
}
