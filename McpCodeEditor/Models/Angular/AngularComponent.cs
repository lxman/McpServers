namespace McpCodeEditor.Models.Angular;

/// <summary>
/// Represents an Angular component with its metadata, structure, and refactoring capabilities
/// </summary>
public class AngularComponent
{
    /// <summary>
    /// Basic component information
    /// </summary>
    public string Name { get; set; } = string.Empty;
    public string Selector { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    
    /// <summary>
    /// Component decorator properties
    /// </summary>
    public string? TemplateUrl { get; set; }
    public string? Template { get; set; } // Inline template
    public List<string> StyleUrls { get; set; } = [];
    public List<string> Styles { get; set; } = []; // Inline styles
    public bool Standalone { get; set; }
    public List<string> Imports { get; set; } = []; // For standalone components
    
    /// <summary>
    /// Component structure and properties
    /// </summary>
    public List<AngularProperty> InputProperties { get; set; } = [];
    public List<AngularProperty> OutputProperties { get; set; } = [];
    public List<AngularProperty> ViewChildren { get; set; } = [];
    public List<AngularProperty> ContentChildren { get; set; } = [];
    public List<AngularMethod> Methods { get; set; } = [];
    public List<AngularLifecycleHook> LifecycleHooks { get; set; } = [];
    
    /// <summary>
    /// Dependency injection and services
    /// </summary>
    public List<AngularDependency> Dependencies { get; set; } = [];
    public List<AngularService> InjectedServices { get; set; } = [];
    
    /// <summary>
    /// Template analysis
    /// </summary>
    public List<string> TemplateProperties { get; set; } = []; // Properties used in template
    public List<string> TemplateMethods { get; set; } = []; // Methods called in template
    public List<string> EventHandlers { get; set; } = []; // Event handlers in template
    
    /// <summary>
    /// Module and routing information
    /// </summary>
    public string? ModulePath { get; set; }
    public List<string> Routes { get; set; } = [];
    public bool IsRoutable { get; set; }
    
    /// <summary>
    /// Component metadata
    /// </summary>
    public bool IsExported { get; set; }
    public string? ExtendsClass { get; set; }
    public List<string> ImplementsInterfaces { get; set; } = [];
    public DateTime LastModified { get; set; }
    public int LineCount { get; set; }
}
