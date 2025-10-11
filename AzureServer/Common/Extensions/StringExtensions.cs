namespace AzureServer.Common.Extensions;

/// <summary>
/// Extension methods for string operations commonly used in Azure DevOps
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
    /// Extracts display name from Azure DevOps user field format "Display Name <email@domain.com>"
    /// </summary>
    public static string ExtractDisplayName(this string? userField)
    {
        if (string.IsNullOrEmpty(userField))
            return string.Empty;

        // Azure DevOps user fields often come in format "Display Name <email@domain.com>"
        int angleIndex = userField.IndexOf('<');
        if (angleIndex > 0)
        {
            return userField[..angleIndex].Trim();
        }
        
        return userField;
    }
    
    /// <summary>
    /// Extracts email from Azure DevOps user field format "Display Name <email@domain.com>"
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
}
