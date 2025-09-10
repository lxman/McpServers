namespace McpCodeEditor.Models.Angular;

/// <summary>
/// Angular component refactoring options
/// </summary>
public class AngularRefactoringOptions
{
    /// <summary>
    /// Method extraction options
    /// </summary>
    public bool ExtractTemplateLogic { get; set; } = true;
    public bool PreserveLifecycleHooks { get; set; } = true;
    public bool MaintainDependencyInjection { get; set; } = true;
    
    /// <summary>
    /// Component restructuring options
    /// </summary>
    public bool SeparateBusinessLogic { get; set; }
    public bool ExtractUtilityMethods { get; set; }
    public bool OptimizeChangeDetection { get; set; }
    
    /// <summary>
    /// Template and style options
    /// </summary>
    public bool UpdateTemplateReferences { get; set; } = true;
    public bool UpdateStyleReferences { get; set; } = true;
    public bool MaintainEventBindings { get; set; } = true;
    
    /// <summary>
    /// Module and import options
    /// </summary>
    public bool UpdateModuleDeclarations { get; set; } = true;
    public bool OptimizeImports { get; set; } = true;
    public bool UpdateRouting { get; set; } = true;
}
