using McpCodeEditor.Models.Validation;

namespace McpCodeEditor.Models.Refactoring.CSharp;

/// <summary>
/// Configuration options for C# method inlining operations.
/// Specifies the method to inline and how the inlining should be performed.
/// </summary>
public class MethodInliningOptions
{
    /// <summary>
    /// Name of the method to inline.
    /// Must be an exact match of the method identifier in the source code.
    /// </summary>
    public string MethodName { get; set; } = string.Empty;

    /// <summary>
    /// Language type for this operation (always CSharp for this class).
    /// </summary>
    public LanguageType Language { get; set; } = LanguageType.CSharp;

    /// <summary>
    /// Whether to preserve original formatting and indentation as much as possible.
    /// When true, attempts to maintain the original code style.
    /// </summary>
    public bool PreserveFormatting { get; set; } = true;

    /// <summary>
    /// Whether to remove empty lines that may result from method removal.
    /// When true, cleans up unnecessary whitespace after inlining.
    /// </summary>
    public bool CleanupEmptyLines { get; set; } = true;

    /// <summary>
    /// Maximum number of call sites that will be inlined.
    /// Helps prevent accidentally inlining methods with too many usages.
    /// Set to 0 for no limit.
    /// </summary>
    public int MaxCallSites { get; set; } = 50;

    /// <summary>
    /// Whether to verify compilation after inlining.
    /// When true, performs basic syntax validation on the result.
    /// </summary>
    public bool VerifyAfterInlining { get; set; } = false;

    /// <summary>
    /// Additional context for the refactoring operation.
    /// </summary>
    public RefactoringContext? Context { get; set; }

    /// <summary>
    /// Validates the method inlining options for correctness.
    /// </summary>
    /// <returns>Validation result with any errors or warnings</returns>
    public MethodInliningValidationResult Validate()
    {
        var result = new MethodInliningValidationResult();

        // Validate method name
        if (string.IsNullOrWhiteSpace(MethodName))
        {
            result.AddError("EMPTY_METHOD_NAME", "Method name is required and cannot be empty");
        }
        else if (!IsValidCSharpIdentifier(MethodName))
        {
            result.AddError("INVALID_METHOD_NAME", $"'{MethodName}' is not a valid C# method name");
        }

        // Validate max call sites
        if (MaxCallSites < 0)
        {
            result.AddError("NEGATIVE_MAX_CALL_SITES", "MaxCallSites cannot be negative");
        }

        // Add warnings for potentially risky configurations
        if (MaxCallSites > 100)
        {
            result.AddWarning("HIGH_CALL_SITE_COUNT", "Inlining methods with more than 100 call sites may result in significant code duplication");
        }

        if (!PreserveFormatting)
        {
            result.AddWarning("FORMATTING_DISABLED", "Disabling formatting preservation may result in inconsistent code style");
        }

        return result;
    }

    /// <summary>
    /// Creates a copy of the options with the specified method name.
    /// </summary>
    /// <param name="methodName">The new method name</param>
    /// <returns>A new MethodInliningOptions instance with the updated method name</returns>
    public MethodInliningOptions WithMethodName(string methodName)
    {
        return new MethodInliningOptions
        {
            MethodName = methodName,
            Language = Language,
            PreserveFormatting = PreserveFormatting,
            CleanupEmptyLines = CleanupEmptyLines,
            MaxCallSites = MaxCallSites,
            VerifyAfterInlining = VerifyAfterInlining,
            Context = Context
        };
    }

    /// <summary>
    /// Creates default options for method inlining.
    /// </summary>
    /// <param name="methodName">The method name to inline</param>
    /// <returns>MethodInliningOptions with sensible defaults</returns>
    public static MethodInliningOptions CreateDefault(string methodName)
    {
        return new MethodInliningOptions
        {
            MethodName = methodName,
            Language = LanguageType.CSharp,
            PreserveFormatting = true,
            CleanupEmptyLines = true,
            MaxCallSites = 50,
            VerifyAfterInlining = false
        };
    }

    /// <summary>
    /// Validates if a string is a valid C# identifier.
    /// </summary>
    /// <param name="identifier">The identifier to validate</param>
    /// <returns>True if the identifier is valid, false otherwise</returns>
    private static bool IsValidCSharpIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return false;

        // Must start with letter or underscore
        if (!char.IsLetter(identifier[0]) && identifier[0] != '_')
            return false;

        // Rest must be letters, digits, or underscores
        for (var i = 1; i < identifier.Length; i++)
        {
            if (!char.IsLetterOrDigit(identifier[i]) && identifier[i] != '_')
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

        return !keywords.Contains(identifier.ToLower());
    }
}
