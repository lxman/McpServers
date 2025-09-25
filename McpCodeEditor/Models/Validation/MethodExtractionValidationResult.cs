using McpCodeEditor.Models.Refactoring.CSharp;

namespace McpCodeEditor.Models.Validation
{
    /// <summary>
    /// Validation result specific to method extraction operations
    /// </summary>
    public class MethodExtractionValidationResult : ValidationResultBase<ExtractedMethodInfo>
    {
        /// <summary>
        /// The language of the code being extracted (C#, TypeScript, etc.)
        /// </summary>
        public string Language { get; set; } = string.Empty;

        /// <summary>
        /// Variables that need to be passed as parameters
        /// </summary>
        public List<ParameterInfo> RequiredParameters { get; set; }

        /// <summary>
        /// Variables that need to be returned
        /// </summary>
        public List<ReturnValueInfo> ReturnValues { get; set; }

        /// <summary>
        /// Complexity score of the extraction (0-100, higher = more complex)
        /// </summary>
        public int ComplexityScore { get; set; }

        /// <summary>
        /// Indicates if ref parameters are needed
        /// </summary>
        public bool RequiresRefParameters { get; set; }

        /// <summary>
        /// Indicates if tuple return is needed
        /// </summary>
        public bool RequiresTupleReturn { get; set; }

        /// <summary>
        /// Suggested method name
        /// </summary>
        public string? SuggestedMethodName { get; set; }

        /// <summary>
        /// Analysis information about the extraction (for compatibility with legacy code)
        /// </summary>
        public CSharpExtractionAnalysis? Analysis { get; set; }

        public MethodExtractionValidationResult()
        {
            RequiredParameters = [];
            ReturnValues = [];
        }

        /// <summary>
        /// Creates a successful method extraction validation result
        /// </summary>
        public static MethodExtractionValidationResult Success(ExtractedMethodInfo methodInfo, string? message = null)
        {
            var result = new MethodExtractionValidationResult();
            result.SetValid(methodInfo, message);
            return result;
        }

        /// <summary>
        /// Creates a failed method extraction validation result
        /// </summary>
        public static MethodExtractionValidationResult Failure(IEnumerable<ValidationError> errors, string? message = null)
        {
            var result = new MethodExtractionValidationResult();
            result.SetInvalid(message);
            foreach (var error in errors)
            {
                result.AddError(error);
            }
            return result;
        }

        /// <summary>
        /// Adds a complexity warning if the extraction is too complex
        /// </summary>
        public void AddComplexityWarning()
        {
            if (ComplexityScore > 70)
            {
                AddWarning("HIGH_COMPLEXITY", 
                    $"Extraction complexity is high (Score: {ComplexityScore}). Consider extracting smaller chunks.");
            }
        }
    }

    /// <summary>
    /// Information about an extracted method
    /// </summary>
    public class ExtractedMethodInfo
    {
        public string MethodSignature { get; set; } = string.Empty;
        public string MethodBody { get; set; } = string.Empty;
        public string MethodCall { get; set; } = string.Empty;
        public int StartLine { get; set; }
        public int EndLine { get; set; }
        public string? ReturnType { get; set; }
        public string? AccessModifier { get; set; }
    }

    /// <summary>
    /// Information about a parameter for the extracted method
    /// </summary>
    public class ParameterInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsRef { get; set; }
        public bool IsOut { get; set; }
        public string? DefaultValue { get; set; }

        public override string ToString()
        {
            var modifiers = new List<string>();
            if (IsRef) modifiers.Add("ref");
            if (IsOut) modifiers.Add("out");
            
            var modifierStr = modifiers.Count > 0 ? string.Join(" ", modifiers) + " " : "";
            var defaultStr = !string.IsNullOrEmpty(DefaultValue) ? $" = {DefaultValue}" : "";
            
            return $"{modifierStr}{Type} {Name}{defaultStr}";
        }
    }

    /// <summary>
    /// Information about a return value from the extracted method
    /// </summary>
    public class ReturnValueInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsModified { get; set; }
        public bool IsUsedAfter { get; set; }

        public override string ToString()
        {
            return $"{Type} {Name}";
        }
    }
}
