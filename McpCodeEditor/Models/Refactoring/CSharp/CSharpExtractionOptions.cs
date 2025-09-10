namespace McpCodeEditor.Models.Refactoring.CSharp;

/// <summary>
/// C# specific options for method extraction operations
/// </summary>
public class CSharpExtractionOptions
{
    /// <summary>
    /// Gets or sets the name of the new method to be extracted
    /// </summary>
    public string NewMethodName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the starting line number (1-based) of the code to extract
    /// </summary>
    public int StartLine { get; set; }

    /// <summary>
    /// Gets or sets the ending line number (1-based) of the code to extract
    /// </summary>
    public int EndLine { get; set; }

    /// <summary>
    /// Gets or sets whether the extracted method should be static
    /// </summary>
    public bool IsStatic { get; set; } = false;

    /// <summary>
    /// Gets or sets the access modifier for the extracted method (public, private, protected, internal)
    /// </summary>
    public string AccessModifier { get; set; } = "private";

    /// <summary>
    /// Gets or sets the return type for the extracted method (null for auto-detection)
    /// </summary>
    public string? ReturnType { get; set; }

    /// <summary>
    /// Gets or sets whether to automatically detect parameters from external variables
    /// </summary>
    public bool AutoDetectParameters { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to add null checks for reference type parameters
    /// </summary>
    public bool AddNullChecks { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to add XML documentation comments to the extracted method
    /// </summary>
    public bool AddDocumentation { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to preserve relative indentation in the extracted method body
    /// </summary>
    public bool PreserveIndentation { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to format the extracted method using Roslyn formatter
    /// </summary>
    public bool FormatCode { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum cyclomatic complexity allowed for extraction
    /// </summary>
    public int MaxCyclomaticComplexity { get; set; } = 15;

    /// <summary>
    /// Gets or sets whether to validate that the selected code doesn't contain returns in the middle of control flow
    /// </summary>
    public bool ValidateControlFlow { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to allow extraction of code with multiple exit points
    /// </summary>
    public bool AllowMultipleExitPoints { get; set; } = false;

    /// <summary>
    /// Gets or sets the target framework version for compatibility checks
    /// </summary>
    public string? TargetFramework { get; set; }

    /// <summary>
    /// Gets or sets additional context data for the extraction operation
    /// </summary>
    public Dictionary<string, object> AdditionalData { get; set; } = new();

    /// <summary>
    /// Validates the extraction options
    /// </summary>
    /// <returns>True if options are valid, false otherwise</returns>
    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(NewMethodName))
            return false;

        if (StartLine < 1 || EndLine < 1 || StartLine > EndLine)
            return false;

        if (!IsValidAccessModifier(AccessModifier))
            return false;

        if (!IsValidMethodName(NewMethodName))
            return false;

        return true;
    }

    /// <summary>
    /// Gets validation errors for the current options
    /// </summary>
    /// <returns>List of validation error messages</returns>
    public List<string> GetValidationErrors()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(NewMethodName))
            errors.Add("Method name is required");

        if (StartLine < 1)
            errors.Add("Start line must be greater than 0");

        if (EndLine < 1)
            errors.Add("End line must be greater than 0");

        if (StartLine > EndLine)
            errors.Add("Start line must be less than or equal to end line");

        if (!IsValidAccessModifier(AccessModifier))
            errors.Add($"Invalid access modifier: {AccessModifier}");

        if (!IsValidMethodName(NewMethodName))
            errors.Add($"Invalid method name: {NewMethodName}");

        if (MaxCyclomaticComplexity < 1)
            errors.Add("Maximum cyclomatic complexity must be greater than 0");

        return errors;
    }

    /// <summary>
    /// Creates a RefactoringContext from these options and additional parameters
    /// </summary>
    /// <param name="filePath">The file path being refactored</param>
    /// <param name="fileContent">The file content</param>
    /// <param name="workspaceRoot">The workspace root path</param>
    /// <returns>A configured RefactoringContext</returns>
    public RefactoringContext ToRefactoringContext(string filePath, string fileContent, string workspaceRoot = "")
    {
        return new RefactoringContext
        {
            FilePath = filePath,
            Language = LanguageType.CSharp,
            WorkspaceRoot = workspaceRoot,
            FileContent = fileContent,
            AdditionalData = new Dictionary<string, object>
            {
                ["extractionOptions"] = this,
                ["startLine"] = StartLine,
                ["endLine"] = EndLine,
                ["methodName"] = NewMethodName,
                ["isStatic"] = IsStatic,
                ["accessModifier"] = AccessModifier,
                ["returnType"] = ReturnType ?? "auto"
            }
        };
    }

    private static bool IsValidAccessModifier(string modifier)
    {
        var validModifiers = new[] { "public", "private", "protected", "internal", "protected internal", "private protected" };
        return validModifiers.Contains(modifier.ToLowerInvariant());
    }

    private static bool IsValidMethodName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        // Must start with letter or underscore
        if (!char.IsLetter(name[0]) && name[0] != '_')
            return false;

        // Rest must be letters, digits, or underscores
        for (var i = 1; i < name.Length; i++)
        {
            if (!char.IsLetterOrDigit(name[i]) && name[i] != '_')
                return false;
        }

        // Check against C# keywords
        string[] keywords =
        [
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
            "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
            "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
            "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
            "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
            "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this",
            "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort",
            "using", "virtual", "void", "volatile", "while", "var"
        ];

        return !keywords.Contains(name.ToLower());
    }
}
