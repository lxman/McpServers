namespace McpCodeEditor.Models.Validation
{
    /// <summary>
    /// Base class for all validation results in the application.
    /// Provides a unified structure for validation outcomes across different domains.
    /// </summary>
    /// <typeparam name="T">The type of data being validated</typeparam>
    public abstract class ValidationResultBase<T>
    {
        /// <summary>
        /// Indicates whether the validation succeeded
        /// </summary>
        public bool IsValid { get; protected set; }

        /// <summary>
        /// The validated data (if validation succeeded)
        /// </summary>
        public T? Data { get; protected set; }

        /// <summary>
        /// Collection of validation errors
        /// </summary>
        public List<ValidationError> Errors { get; protected set; }

        /// <summary>
        /// Collection of validation warnings (non-blocking issues)
        /// </summary>
        public List<ValidationWarning> Warnings { get; protected set; }

        /// <summary>
        /// Optional message providing additional context
        /// </summary>
        public string? Message { get; protected set; }

        /// <summary>
        /// Timestamp of when validation was performed
        /// </summary>
        public DateTime ValidationTimestamp { get; protected set; }

        protected ValidationResultBase()
        {
            Errors = [];
            Warnings = [];
            ValidationTimestamp = DateTime.UtcNow;
        }

        /// <summary>
        /// Sets the validation result as valid and assigns the data
        /// </summary>
        protected void SetValid(T data, string? message = null)
        {
            IsValid = true;
            Data = data;
            Message = message;
        }

        /// <summary>
        /// Sets the validation result as invalid
        /// </summary>
        protected void SetInvalid(string? message = null)
        {
            IsValid = false;
            Message = message;
        }

        /// <summary>
        /// Adds a validation error to the result
        /// </summary>
        public void AddError(ValidationError error)
        {
            Errors.Add(error);
            IsValid = false;
        }

        /// <summary>
        /// Adds a validation error with code and description
        /// </summary>
        public void AddError(string code, string description, string? propertyName = null, 
            int? lineNumber = null, int? columnNumber = null)
        {
            AddError(new ValidationError(code, description, propertyName, lineNumber, columnNumber));
        }

        /// <summary>
        /// Adds a validation warning to the result
        /// </summary>
        public void AddWarning(ValidationWarning warning)
        {
            Warnings.Add(warning);
        }

        /// <summary>
        /// Adds a warning to the validation result
        /// </summary>
        public void AddWarning(string code, string description)
        {
            Warnings.Add(new ValidationWarning(code, description));
        }

        /// <summary>
        /// Creates a successful validation result
        /// </summary>
        public static TResult Success<TResult>(T data, string? message = null) 
            where TResult : ValidationResultBase<T>, new()
        {
            var result = new TResult();
            result.SetValid(data, message);
            return result;
        }

        /// <summary>
        /// Creates a failed validation result
        /// </summary>
        public static TResult Failure<TResult>(IEnumerable<ValidationError> errors, string? message = null)
            where TResult : ValidationResultBase<T>, new()
        {
            var result = new TResult();
            result.SetInvalid(message);
            foreach (ValidationError error in errors)
            {
                result.AddError(error);
            }
            return result;
        }

        /// <summary>
        /// Creates a failed validation result with a single error
        /// </summary>
        public static TResult Failure<TResult>(string errorCode, string errorDescription, string? message = null)
            where TResult : ValidationResultBase<T>, new()
        {
            var result = new TResult();
            result.SetInvalid(message);
            result.AddError(errorCode, errorDescription);
            return result;
        }

        /// <summary>
        /// Gets a formatted error message combining all errors
        /// </summary>
        public string GetFormattedErrorMessage()
        {
            if (Errors.Count == 0)
                return string.Empty;

            return string.Join(Environment.NewLine, Errors.Select(e => e.ToString()));
        }

        /// <summary>
        /// Gets a formatted warning message combining all warnings
        /// </summary>
        public string GetFormattedWarningMessage()
        {
            if (Warnings.Count == 0)
                return string.Empty;

            return string.Join(Environment.NewLine, Warnings.Select(w => w.ToString()));
        }
    }

    /// <summary>
    /// Represents a validation error
    /// </summary>
    public class ValidationError
    {
        public string Code { get; }
        public string Description { get; }
        public string? PropertyName { get; }
        public int? LineNumber { get; }
        public int? ColumnNumber { get; }

        public ValidationError(string code, string description, string? propertyName = null, 
            int? lineNumber = null, int? columnNumber = null)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            PropertyName = propertyName;
            LineNumber = lineNumber;
            ColumnNumber = columnNumber;
        }

        public override string ToString()
        {
            var parts = new List<string> { $"[{Code}] {Description}" };
            
            if (!string.IsNullOrEmpty(PropertyName))
                parts.Add($"Property: {PropertyName}");
            
            if (LineNumber.HasValue)
                parts.Add($"Line: {LineNumber}");
            
            if (ColumnNumber.HasValue)
                parts.Add($"Column: {ColumnNumber}");

            return string.Join(", ", parts);
        }
    }

    /// <summary>
    /// Represents a validation warning (non-blocking issue)
    /// </summary>
    public class ValidationWarning
    {
        public string Code { get; }
        public string Description { get; }

        public ValidationWarning(string code, string description)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Description = description ?? throw new ArgumentNullException(nameof(description));
        }

        public override string ToString()
        {
            return $"[{Code}] {Description}";
        }
    }
}
