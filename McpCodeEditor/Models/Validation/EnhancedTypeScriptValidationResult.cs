namespace McpCodeEditor.Models.Validation
{
    /// <summary>
    /// Enhanced validation result for TypeScript code operations with comprehensive analysis
    /// (Renamed from TypeScriptValidationResult to resolve naming conflict with Models.TypeScript version)
    /// </summary>
    public class EnhancedTypeScriptValidationResult : ValidationResultBase<EnhancedTypeScriptValidationDetails>
    {
        /// <summary>
        /// Creates a successful TypeScript validation result
        /// </summary>
        /// <param name="details">Optional TypeScript validation details</param>
        public static EnhancedTypeScriptValidationResult Valid(EnhancedTypeScriptValidationDetails? details = null)
        {
            return new EnhancedTypeScriptValidationResult
            {
                IsValid = true,
                Data = details,
                Message = "TypeScript validation successful"
            };
        }

        /// <summary>
        /// Creates a failed TypeScript validation result
        /// </summary>
        /// <param name="errorMessage">Primary error message</param>
        /// <param name="details">Optional TypeScript validation details</param>
        public static EnhancedTypeScriptValidationResult Invalid(string errorMessage, EnhancedTypeScriptValidationDetails? details = null)
        {
            var result = new EnhancedTypeScriptValidationResult
            {
                IsValid = false,
                Data = details,
                Message = errorMessage
            };
            result.Errors.Add(new ValidationError("TS_VALIDATION_ERROR", errorMessage));
            return result;
        }
    }

    /// <summary>
    /// Enhanced TypeScript specific validation details with comprehensive analysis capabilities
    /// </summary>
    public class EnhancedTypeScriptValidationDetails
    {
        /// <summary>
        /// Gets or sets the source code that was validated
        /// </summary>
        public string? SourceCode { get; set; }

        /// <summary>
        /// Gets or sets the file path being validated
        /// </summary>
        public string? FilePath { get; set; }

        /// <summary>
        /// Gets or sets the TypeScript compiler errors
        /// </summary>
        public List<string> CompilerErrors { get; init; } = new List<string>();

        /// <summary>
        /// Gets or sets syntax validation errors
        /// </summary>
        public List<string> SyntaxErrors { get; init; } = new List<string>();

        /// <summary>
        /// Gets or sets type checking errors
        /// </summary>
        public List<string> TypeErrors { get; init; } = new List<string>();

        /// <summary>
        /// Gets or sets AST analysis results
        /// </summary>
        public string? AstAnalysis { get; set; }

        /// <summary>
        /// Gets or sets scope detection results
        /// </summary>
        public string? ScopeDetection { get; set; }

        /// <summary>
        /// Gets or sets expression boundary detection results
        /// </summary>
        public string? ExpressionBoundaryDetection { get; set; }

        /// <summary>
        /// Gets or sets variable declaration analysis
        /// </summary>
        public string? VariableDeclarationAnalysis { get; set; }

        /// <summary>
        /// Gets or sets function extraction analysis
        /// </summary>
        public string? FunctionExtractionAnalysis { get; set; }

        /// <summary>
        /// Gets or sets import/export analysis
        /// </summary>
        public string? ImportExportAnalysis { get; set; }

        /// <summary>
        /// Gets whether TypeScript compiler errors were found
        /// </summary>
        public bool HasCompilerErrors => CompilerErrors.Count > 0;

        /// <summary>
        /// Gets whether syntax errors were found
        /// </summary>
        public bool HasSyntaxErrors => SyntaxErrors.Count > 0;

        /// <summary>
        /// Gets whether type errors were found
        /// </summary>
        public bool HasTypeErrors => TypeErrors.Count > 0;
    }
}
