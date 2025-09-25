namespace McpCodeEditor.Models.Validation
{
    /// <summary>
    /// Validation result specific to method inlining operations
    /// </summary>
    public class MethodInliningValidationResult : ValidationResultBase<InlinedMethodInfo>
    {
        /// <summary>
        /// The language of the code being inlined (C#, TypeScript, etc.)
        /// </summary>
        public string Language { get; set; } = string.Empty;

        /// <summary>
        /// Number of call sites that will be affected
        /// </summary>
        public int CallSiteCount { get; set; }

        /// <summary>
        /// List of affected files
        /// </summary>
        public List<string> AffectedFiles { get; set; }

        /// <summary>
        /// Indicates if the method has side effects that may change behavior when inlined
        /// </summary>
        public bool HasSideEffects { get; set; }

        /// <summary>
        /// Indicates if the method is recursive (cannot be inlined)
        /// </summary>
        public bool IsRecursive { get; set; }

        /// <summary>
        /// Indicates if the method is virtual/override (requires careful inlining)
        /// </summary>
        public bool IsVirtual { get; set; }

        /// <summary>
        /// Estimated lines of code that will be duplicated
        /// </summary>
        public int EstimatedDuplication { get; set; }

        public MethodInliningValidationResult()
        {
            AffectedFiles = [];
        }

        /// <summary>
        /// Creates a successful method inlining validation result
        /// </summary>
        public static MethodInliningValidationResult Success(InlinedMethodInfo inlineInfo, string? message = null)
        {
            var result = new MethodInliningValidationResult();
            result.SetValid(inlineInfo, message);
            return result;
        }

        /// <summary>
        /// Creates a failed method inlining validation result
        /// </summary>
        public static MethodInliningValidationResult Failure(IEnumerable<ValidationError> errors, string? message = null)
        {
            var result = new MethodInliningValidationResult();
            result.SetInvalid(message);
            foreach (var error in errors)
            {
                result.AddError(error);
            }
            return result;
        }

        /// <summary>
        /// Adds warnings for potential issues with inlining
        /// </summary>
        public void AddInliningWarnings()
        {
            if (IsRecursive)
            {
                Errors.Add(new ValidationError("RECURSIVE_METHOD", 
                    "Cannot inline recursive methods"));
            }

            if (IsVirtual)
            {
                AddWarning("VIRTUAL_METHOD", 
                    "Inlining virtual/override method may change polymorphic behavior");
            }

            if (HasSideEffects)
            {
                AddWarning("SIDE_EFFECTS", 
                    "Method has side effects that may change execution order when inlined");
            }

            if (EstimatedDuplication > 50)
            {
                AddWarning("HIGH_DUPLICATION", 
                    $"Inlining will duplicate approximately {EstimatedDuplication} lines of code across {CallSiteCount} call sites");
            }

            if (CallSiteCount > 10)
            {
                AddWarning("MANY_CALL_SITES", 
                    $"Method is called from {CallSiteCount} locations. Consider if inlining is appropriate.");
            }
        }
    }

    /// <summary>
    /// Information about an inlined method
    /// </summary>
    public class InlinedMethodInfo
    {
        public string MethodName { get; set; } = string.Empty;
        public string OriginalSignature { get; set; } = string.Empty;
        public string MethodBody { get; set; } = string.Empty;
        public List<CallSiteInfo> CallSites { get; set; }

        public InlinedMethodInfo()
        {
            CallSites = [];
        }
    }

    /// <summary>
    /// Information about a call site where the method will be inlined
    /// </summary>
    public class CallSiteInfo
    {
        public string FilePath { get; set; } = string.Empty;
        public int Line { get; set; }
        public int Column { get; set; }
        public string CallExpression { get; set; } = string.Empty;
        public string? ReplacementCode { get; set; }
        public Dictionary<string, string> ParameterMappings { get; set; }

        public CallSiteInfo()
        {
            ParameterMappings = new Dictionary<string, string>();
        }

        public override string ToString()
        {
            return $"{FilePath}:{Line}:{Column} - {CallExpression}";
        }
    }
}
