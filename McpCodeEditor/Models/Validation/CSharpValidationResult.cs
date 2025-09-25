namespace McpCodeEditor.Models.Validation
{
    /// <summary>
    /// Validation result specific to C# code operations
    /// </summary>
    public class CSharpValidationResult : ValidationResultBase<string>
    {
        /// <summary>
        /// C#-specific validation context (e.g., method, class, namespace)
        /// </summary>
        public string? Context { get; set; }

        /// <summary>
        /// Roslyn diagnostic information if available
        /// </summary>
        public List<RoslynDiagnostic> RoslynDiagnostics { get; set; }

        /// <summary>
        /// Indicates if the code compiles successfully
        /// </summary>
        public bool Compiles { get; set; }

        public CSharpValidationResult()
        {
            RoslynDiagnostics = [];
        }

        /// <summary>
        /// Creates a successful C# validation result
        /// </summary>
        public static CSharpValidationResult Success(string code, string? message = null)
        {
            return new CSharpValidationResult
            {
                IsValid = true,
                Data = code,
                Message = message,
                Compiles = true
            };
        }

        /// <summary>
        /// Creates a failed C# validation result
        /// </summary>
        public static CSharpValidationResult Failure(IEnumerable<ValidationError> errors, string? message = null)
        {
            var result = new CSharpValidationResult
            {
                IsValid = false,
                Message = message,
                Compiles = false
            };
            result.Errors.AddRange(errors);
            return result;
        }

        /// <summary>
        /// Adds a Roslyn diagnostic to the result
        /// </summary>
        public void AddRoslynDiagnostic(string id, string severity, string message, int? line = null, int? column = null)
        {
            RoslynDiagnostics.Add(new RoslynDiagnostic(id, severity, message, line, column));
        }
    }

    /// <summary>
    /// Represents a Roslyn diagnostic message
    /// </summary>
    public class RoslynDiagnostic
    {
        public string Id { get; }
        public string Severity { get; }
        public string Message { get; }
        public int? Line { get; }
        public int? Column { get; }

        public RoslynDiagnostic(string id, string severity, string message, int? line = null, int? column = null)
        {
            Id = id;
            Severity = severity;
            Message = message;
            Line = line;
            Column = column;
        }

        public override string ToString()
        {
            var location = Line.HasValue ? $" at line {Line}" : "";
            if (Column.HasValue)
                location += $", column {Column}";
            return $"{Severity} {Id}: {Message}{location}";
        }
    }
}
