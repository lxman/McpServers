namespace McpCodeEditor.Models.Refactoring.CSharp;

/// <summary>
/// Options for encapsulating a field in C# code
/// </summary>
public class FieldEncapsulationOptions
{
    /// <summary>
    /// Name of the field to encapsulate
    /// </summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// Name for the generated property (optional - will be auto-generated if not provided)
    /// </summary>
    public string? PropertyName { get; set; }

    /// <summary>
    /// Whether to use auto-property syntax ({ get; set; }) or full property with backing field
    /// </summary>
    public bool UseAutoProperty { get; set; } = true;

    /// <summary>
    /// Access modifier for the generated property
    /// </summary>
    public string PropertyAccessModifier { get; set; } = "public";

    /// <summary>
    /// Access modifier for the backing field (when not using auto-property)
    /// </summary>
    public string BackingFieldAccessModifier { get; set; } = "private";

    /// <summary>
    /// Prefix for the backing field name (when not using auto-property)
    /// </summary>
    public string BackingFieldPrefix { get; set; } = "_";

    /// <summary>
    /// Whether to generate getter for the property
    /// </summary>
    public bool GenerateGetter { get; set; } = true;

    /// <summary>
    /// Whether to generate setter for the property
    /// </summary>
    public bool GenerateSetter { get; set; } = true;

    /// <summary>
    /// Access modifier for the getter (if different from property)
    /// </summary>
    public string? GetterAccessModifier { get; set; }

    /// <summary>
    /// Access modifier for the setter (if different from property)
    /// </summary>
    public string? SetterAccessModifier { get; set; }

    /// <summary>
    /// Whether to update all references to the field within the class
    /// </summary>
    public bool UpdateReferences { get; set; } = true;

    /// <summary>
    /// Whether to add validation to the setter
    /// </summary>
    public bool AddValidation { get; set; } = false;

    /// <summary>
    /// Validation code to add to the setter (when AddValidation is true)
    /// </summary>
    public string? ValidationCode { get; set; }

    /// <summary>
    /// Whether to add XML documentation to the generated property
    /// </summary>
    public bool AddDocumentation { get; set; } = false;

    /// <summary>
    /// Custom documentation for the property
    /// </summary>
    public string? PropertyDocumentation { get; set; }

    /// <summary>
    /// Whether to preserve field initialization in auto-properties
    /// </summary>
    public bool PreserveInitialization { get; set; } = true;

    /// <summary>
    /// Whether to make the property virtual
    /// </summary>
    public bool MakeVirtual { get; set; } = false;

    /// <summary>
    /// Whether to check for field usage in constructors and handle appropriately
    /// </summary>
    public bool HandleConstructorUsage { get; set; } = true;

    /// <summary>
    /// Whether to validate that the field exists and is public before encapsulation
    /// </summary>
    public bool ValidateFieldAccess { get; set; } = true;
}
