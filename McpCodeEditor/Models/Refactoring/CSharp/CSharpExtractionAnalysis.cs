namespace McpCodeEditor.Models.Refactoring.CSharp
{
    /// <summary>
    /// Analysis of C# code to be extracted
    /// </summary>
    public class CSharpExtractionAnalysis
    {
        /// <summary>
        /// Gets or sets whether the code contains return statements
        /// </summary>
        public bool HasReturnStatements { get; set; }

        /// <summary>
        /// Gets or sets the cyclomatic complexity of the code
        /// </summary>
        public int CyclomaticComplexity { get; set; }

        /// <summary>
        /// Gets or sets the name of the containing method
        /// </summary>
        public string ContainingMethodName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the code has complex control flow
        /// </summary>
        public bool HasComplexControlFlow { get; set; }

        /// <summary>
        /// Gets or sets external variables referenced in the code
        /// </summary>
        public List<string> ExternalVariables { get; set; } = [];

        /// <summary>
        /// Gets or sets suggested parameters based on external dependencies
        /// </summary>
        public List<string> SuggestedParameters { get; set; } = [];

        /// <summary>
        /// Gets or sets suggested return type based on analysis
        /// </summary>
        public string? SuggestedReturnType { get; set; }

        /// <summary>
        /// Gets or sets the reason for the suggested return type
        /// </summary>
        public string? ReturnTypeReason { get; set; }

        /// <summary>
        /// Gets or sets whether the extracted method requires a return value
        /// </summary>
        public bool RequiresReturnValue { get; set; }

        /// <summary>
        /// Gets or sets variables that are modified within the extracted code
        /// </summary>
        public List<string> ModifiedVariables { get; set; } = [];

        /// <summary>
        /// Gets or sets variables that are declared locally within the extracted code
        /// </summary>
        public List<string> LocalVariables { get; set; } = [];
    }

    /// <summary>
    /// Result of C# method extraction validation
    /// </summary>
    public class CSharpValidationResult
    {
        /// <summary>
        /// Gets or sets whether the validation passed
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets or sets validation error messages
        /// </summary>
        public List<string> Errors { get; set; } = [];

        /// <summary>
        /// Gets or sets validation warning messages
        /// </summary>
        public List<string> Warnings { get; set; } = [];

        /// <summary>
        /// Gets or sets analysis information about the extraction
        /// </summary>
        public CSharpExtractionAnalysis? Analysis { get; set; }
    }
}
