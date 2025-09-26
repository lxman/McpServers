namespace McpCodeEditor.Services.Utilities;

/// <summary>
/// Utility service for identifier validation and name generation.
/// Extracted from RefactoringService as part of SOLID refactoring (Slice VIII).
/// </summary>
public static class IdentifierValidation
{
    /// <summary>
    /// Check if a string is a valid C# identifier
    /// </summary>
    public static bool IsValidIdentifier(string name)
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

    /// <summary>
    /// Check if TypeScript identifier is valid
    /// </summary>
    public static bool IsValidTypeScriptIdentifier(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        // Must start with letter, underscore, or $
        if (!char.IsLetter(name[0]) && name[0] != '_' && name[0] != '$')
            return false;

        // Rest must be letters, digits, underscores, or $
        for (var i = 1; i < name.Length; i++)
        {
            if (!char.IsLetterOrDigit(name[i]) && name[i] != '_' && name[i] != '$')
                return false;
        }

        // Check against TypeScript/JavaScript keywords
        string[] keywords =
        [
            "break", "case", "catch", "class", "const", "continue", "debugger", "default", "delete",
            "do", "else", "enum", "export", "extends", "false", "finally", "for", "function",
            "if", "import", "in", "instanceof", "let", "new", "null", "return", "super", "switch",
            "this", "throw", "true", "try", "typeof", "var", "void", "while", "with", "yield",
            "abstract", "implements", "interface", "package", "private", "protected", "public", "static"
        ];

        return !keywords.Contains(name.ToLower());
    }

    /// <summary>
    /// Generate a meaningful variable name from an expression
    /// </summary>
    public static string GenerateVariableName(string expression)
    {
        // Simple heuristics for generating variable names
        string cleaned = expression.Trim();

        // Handle method calls
        if (cleaned.Contains('(') && cleaned.Contains(')'))
        {
            string methodPart = cleaned[..cleaned.IndexOf('(')];
            string lastMethod = methodPart.Split('.').Last();
            return ToCamelCase($"{lastMethod}Result");
        }

        // Handle property access
        if (cleaned.Contains('.'))
        {
            string[] parts = cleaned.Split('.');
            if (parts.Length >= 2)
            {
                return ToCamelCase($"{parts[^2]}{parts[^1]}");
            }
        }

        // Handle arithmetic expressions
        if (cleaned.Contains('+') || cleaned.Contains('-') || cleaned.Contains('*') || cleaned.Contains('/'))
        {
            return "calculation";
        }

        // Handle string literals
        if (cleaned.StartsWith('"') && cleaned.EndsWith('"'))
        {
            return "text";
        }

        // Handle numeric literals
        if (int.TryParse(cleaned, out _) || double.TryParse(cleaned, out _))
        {
            return "value";
        }

        // Default fallback
        return "temp";
    }

    /// <summary>
    /// Generate TypeScript variable name from expression
    /// </summary>
    public static string GenerateTypeScriptVariableName(string expression)
    {
        // Use same logic as C# but ensure camelCase
        return GenerateVariableName(expression);
    }

    /// <summary>
    /// Generate a property name from a field name (PascalCase)
    /// </summary>
    public static string GeneratePropertyName(string fieldName)
    {
        string trimmed = fieldName.TrimStart('_');
        if (string.IsNullOrEmpty(trimmed))
            return "Property";

        return char.ToUpper(trimmed[0]) + trimmed[1..];
    }

    /// <summary>
    /// Convert string to camelCase
    /// </summary>
    public static string ToCamelCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "temp";

        string result = input.Trim();
        if (result.Length == 0)
            return "temp";

        // Remove non-alphanumeric characters and capitalize next letter
        var chars = new List<char>();
        var capitalizeNext = false;

        for (var i = 0; i < result.Length; i++)
        {
            char c = result[i];
            if (char.IsLetterOrDigit(c))
            {
                if (capitalizeNext && char.IsLetter(c))
                {
                    chars.Add(char.ToUpper(c));
                    capitalizeNext = false;
                }
                else if (chars.Count == 0 && char.IsLetter(c))
                {
                    chars.Add(char.ToLower(c)); // First letter lowercase for camelCase
                }
                else
                {
                    chars.Add(c);
                }
            }
            else
            {
                capitalizeNext = true;
            }
        }

        var final = new string(chars.ToArray());
        return string.IsNullOrEmpty(final) ? "temp" : final;
    }
}
