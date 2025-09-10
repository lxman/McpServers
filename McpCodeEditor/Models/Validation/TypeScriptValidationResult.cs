namespace McpCodeEditor.Models.Validation
{
    /// <summary>
    /// Validation result specific to TypeScript code operations
    /// </summary>
    public class TypeScriptValidationResult : ValidationResultBase<string>
    {
        /// <summary>
        /// TypeScript-specific validation context (e.g., function, class, module)
        /// </summary>
        public string? Context { get; set; }

        /// <summary>
        /// TypeScript compiler diagnostics if available
        /// </summary>
        public List<TypeScriptDiagnostic> CompilerDiagnostics { get; set; }

        /// <summary>
        /// Indicates if the code passes TypeScript type checking
        /// </summary>
        public bool TypeChecks { get; set; }

        /// <summary>
        /// ESLint or other linter issues if available
        /// </summary>
        public List<LinterIssue> LinterIssues { get; set; }

        public TypeScriptValidationResult()
        {
            CompilerDiagnostics = new List<TypeScriptDiagnostic>();
            LinterIssues = new List<LinterIssue>();
        }

        /// <summary>
        /// Creates a successful TypeScript validation result
        /// </summary>
        public static TypeScriptValidationResult Success(string code, string? message = null)
        {
            return new TypeScriptValidationResult
            {
                IsValid = true,
                Data = code,
                Message = message,
                TypeChecks = true
            };
        }

        /// <summary>
        /// Creates a failed TypeScript validation result
        /// </summary>
        public static TypeScriptValidationResult Failure(IEnumerable<ValidationError> errors, string? message = null)
        {
            var result = new TypeScriptValidationResult
            {
                IsValid = false,
                Message = message,
                TypeChecks = false
            };
            result.Errors.AddRange(errors);
            return result;
        }

        /// <summary>
        /// Adds a TypeScript compiler diagnostic
        /// </summary>
        public void AddCompilerDiagnostic(int code, string category, string message, int? line = null, int? column = null)
        {
            CompilerDiagnostics.Add(new TypeScriptDiagnostic(code, category, message, line, column));
        }

        /// <summary>
        /// Adds a linter issue
        /// </summary>
        public void AddLinterIssue(string rule, string severity, string message, int? line = null)
        {
            LinterIssues.Add(new LinterIssue(rule, severity, message, line));
        }
    }

    /// <summary>
    /// Represents a TypeScript compiler diagnostic
    /// </summary>
    public class TypeScriptDiagnostic
    {
        public int Code { get; }
        public string Category { get; }
        public string Message { get; }
        public int? Line { get; }
        public int? Column { get; }

        public TypeScriptDiagnostic(int code, string category, string message, int? line = null, int? column = null)
        {
            Code = code;
            Category = category;
            Message = message;
            Line = line;
            Column = column;
        }

        public override string ToString()
        {
            string location = Line.HasValue ? $" at line {Line}" : "";
            if (Column.HasValue)
                location += $", column {Column}";
            return $"TS{Code}: {Message}{location}";
        }
    }

    /// <summary>
    /// Represents a linter issue (ESLint, TSLint, etc.)
    /// </summary>
    public class LinterIssue
    {
        public string Rule { get; }
        public string Severity { get; }
        public string Message { get; }
        public int? Line { get; }

        public LinterIssue(string rule, string severity, string message, int? line = null)
        {
            Rule = rule;
            Severity = severity;
            Message = message;
            Line = line;
        }

        public override string ToString()
        {
            string location = Line.HasValue ? $" at line {Line}" : "";
            return $"[{Severity}] {Rule}: {Message}{location}";
        }
    }
}
