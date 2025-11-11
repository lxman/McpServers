namespace Mcp.Common.Core.Extensions;

/// <summary>
/// Extension methods for string operations commonly used across MCP servers
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Checks if a string is null or empty
    /// </summary>
    public static bool IsNullOrEmpty(this string? value)
    {
        return string.IsNullOrEmpty(value);
    }

    /// <summary>
    /// Checks if a string is null, empty, or whitespace
    /// </summary>
    public static bool IsNullOrWhiteSpace(this string? value)
    {
        return string.IsNullOrWhiteSpace(value);
    }

    /// <summary>
    /// Returns the string or a default value if null or empty
    /// </summary>
    public static string OrDefault(this string? value, string defaultValue = "")
    {
        return string.IsNullOrEmpty(value) ? defaultValue : value;
    }

    /// <summary>
    /// Truncates a string to the specified length with optional ellipsis
    /// </summary>
    public static string Truncate(this string value, int maxLength, bool addEllipsis = true)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            return value;

        string truncated = value[..maxLength];
        return addEllipsis ? truncated + "..." : truncated;
    }

    /// <summary>
    /// Extracts display name from email format "Display Name &lt;email@domain.com&gt;"
    /// </summary>
    public static string ExtractDisplayName(this string? userField)
    {
        if (string.IsNullOrEmpty(userField))
            return string.Empty;

        int angleIndex = userField.IndexOf('<');
        if (angleIndex > 0)
        {
            return userField[..angleIndex].Trim();
        }

        return userField;
    }

    /// <summary>
    /// Extracts email from format "Display Name &lt;email@domain.com&gt;"
    /// </summary>
    public static string ExtractEmail(this string? userField)
    {
        if (string.IsNullOrEmpty(userField))
            return string.Empty;

        int startIndex = userField.IndexOf('<');
        int endIndex = userField.IndexOf('>');

        if (startIndex >= 0 && endIndex > startIndex)
        {
            return userField.Substring(startIndex + 1, endIndex - startIndex - 1);
        }

        return string.Empty;
    }

    /// <summary>
    /// Converts a string to a safe filename by removing invalid characters
    /// </summary>
    public static string ToSafeFileName(this string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        char[] invalidChars = Path.GetInvalidFileNameChars();
        return string.Concat(value.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// Converts a string to title case (first letter of each word capitalized)
    /// </summary>
    public static string ToTitleCase(this string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value.ToLower());
    }

    /// <summary>
    /// Ensures a string ends with a specific character/string
    /// </summary>
    public static string EnsureEndsWith(this string value, string suffix)
    {
        if (string.IsNullOrEmpty(value))
            return suffix;

        return value.EndsWith(suffix) ? value : value + suffix;
    }

    /// <summary>
    /// Ensures a string starts with a specific character/string
    /// </summary>
    public static string EnsureStartsWith(this string value, string prefix)
    {
        if (string.IsNullOrEmpty(value))
            return prefix;

        return value.StartsWith(prefix) ? value : prefix + value;
    }
}
