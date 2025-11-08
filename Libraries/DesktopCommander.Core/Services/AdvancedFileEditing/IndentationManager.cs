using DesktopCommander.Core.Services.AdvancedFileEditing.Models;

namespace DesktopCommander.Core.Services.AdvancedFileEditing;

public class IndentationManager
{
    /// <summary>
    /// Detects the primary indentation style used in a file
    /// </summary>
    public static IndentationInfo DetectFileIndentation(string[] lines)
    {
        List<IndentationInfo> indentationSamples =
            (from line in lines.Take(100)
                where !string.IsNullOrWhiteSpace(line)
                select IndentationInfo.DetectFromLine(line)
                into info
                where info.Level > 0
                select info).ToList();

        if (indentationSamples.Count == 0)
            return new IndentationInfo(IndentationType.Spaces, 4); // Default
            
        // Determine the most common indentation type
        var typeGroups = indentationSamples.GroupBy(i => i.Type).ToArray();
        var dominantType = typeGroups.OrderByDescending(g => g.Count()).First().Key;
        
        // For spaces, determine the most common size
        if (dominantType == IndentationType.Spaces)
        {
            var spaceSamples = indentationSamples.Where(i => i.Type == IndentationType.Spaces).ToArray();
            var sizeGroups = spaceSamples.GroupBy(i => i.Size).ToArray();
            var dominantSize = sizeGroups.OrderByDescending(g => g.Count()).First().Key;
            
            return new IndentationInfo(IndentationType.Spaces, dominantSize);
        }
        
        return new IndentationInfo(dominantType, dominantType == IndentationType.Tabs ? 1 : 4);
    }
    
    /// <summary>
    /// Gets the indentation level at a specific line
    /// </summary>
    public static int GetIndentationLevel(string[] lines, int lineNumber)
    {
        if (lineNumber < 1 || lineNumber > lines.Length)
            return 0;
            
        var line = lines[lineNumber - 1];
        var info = IndentationInfo.DetectFromLine(line);
        return info.Level;
    }
    
    /// <summary>
    /// Fixes the indentation of content to match the target level and style
    /// </summary>
    public static string FixIndentation(string content, IndentationInfo targetStyle, int targetLevel)
    {
        if (string.IsNullOrEmpty(content))
            return content;
            
        var lines = content.Split(['\n', '\r'], StringSplitOptions.None)
                          .Where(line => !line.Equals("\r"))
                          .ToArray();
                          
        return FixIndentation(lines, targetStyle, targetLevel);
    }
    
    /// <summary>
    /// Fixes the indentation of multiple lines to match the target level and style
    /// </summary>
    public static string FixIndentation(string[] lines, IndentationInfo targetStyle, int targetLevel)
    {
        if (lines.Length == 0)
            return string.Empty;
            
        var result = new List<string>();
        
        // Find the minimum indentation level in the content (to preserve relative indentation)
        var minIndentLevel = int.MaxValue;
        foreach (var line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                var info = IndentationInfo.DetectFromLine(line);
                minIndentLevel = Math.Min(minIndentLevel, info.Level);
            }
        }
        
        if (minIndentLevel == int.MaxValue)
            minIndentLevel = 0;
            
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                result.Add(line); // Preserve empty lines as-is
            }
            else
            {
                var currentInfo = IndentationInfo.DetectFromLine(line);
                var relativeLevel = currentInfo.Level - minIndentLevel;
                var newLevel = targetLevel + relativeLevel;
                
                var newIndent = targetStyle.AtLevel(newLevel).GetIndentString();
                var contentPart = line.TrimStart();
                
                result.Add(newIndent + contentPart);
            }
        }
        
        return string.Join('\n', result);
    }
    
    /// <summary>
    /// Determines the appropriate indentation level for inserting content at a specific location
    /// </summary>
    public static int DetermineInsertionIndentLevel(string[] lines, int insertAfterLine)
    {
        // Look at surrounding lines to determine appropriate indentation
        var contextLines = new List<int>();
        
        // Check the line we're inserting after
        if (insertAfterLine >= 1 && insertAfterLine <= lines.Length)
        {
            contextLines.Add(insertAfterLine);
        }
        
        // Check the next few lines
        for (var i = insertAfterLine + 1; i <= Math.Min(insertAfterLine + 3, lines.Length); i++)
        {
            contextLines.Add(i);
        }
        
        // Find the most appropriate indentation level
        var indentLevels = contextLines
            .Where(lineNum => lineNum >= 1 && lineNum <= lines.Length)
            .Select(lineNum => lines[lineNum - 1])
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => IndentationInfo.DetectFromLine(line).Level)
            .Where(level => level >= 0)
            .ToArray();
            
        if (indentLevels.Length == 0)
            return 0;
            
        // Return the most common indentation level in the context
        return indentLevels.GroupBy(level => level)
                          .OrderByDescending(g => g.Count())
                          .First().Key;
    }
    
    /// <summary>
    /// Checks if content follows consistent indentation rules
    /// </summary>
    public static (bool isConsistent, string? issues) ValidateIndentation(string content, IndentationInfo expectedStyle)
    {
        var lines = content.Split(['\n', '\r'], StringSplitOptions.None)
                          .Where(line => !line.Equals("\r") && !string.IsNullOrWhiteSpace(line))
                          .ToArray();
                          
        var issues = new List<string>();
        
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var info = IndentationInfo.DetectFromLine(line);
            
            // Check for mixed indentation
            if (expectedStyle.Type == IndentationType.Spaces && info.Type != IndentationType.Spaces)
            {
                issues.Add($"Line {i + 1}: Expected spaces but found {info.Type.ToString().ToLower()}");
            }
            else if (expectedStyle.Type == IndentationType.Tabs && info.Type != IndentationType.Tabs)
            {
                issues.Add($"Line {i + 1}: Expected tabs but found {info.Type.ToString().ToLower()}");
            }
            
            // Check for incorrect indentation size (for spaces)
            if (expectedStyle.Type == IndentationType.Spaces && info.Type == IndentationType.Spaces)
            {
                if (info.Level > 0 && (info.Level * info.Size) % expectedStyle.Size != 0)
                {
                    issues.Add($"Line {i + 1}: Indentation not aligned to {expectedStyle.Size}-space boundaries");
                }
            }
        }
        
        return (issues.Count == 0, issues.Count > 0 ? string.Join("; ", issues) : null);
    }
}