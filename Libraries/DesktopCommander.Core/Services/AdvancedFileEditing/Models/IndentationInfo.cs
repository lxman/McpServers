namespace DesktopCommander.Core.Services.AdvancedFileEditing.Models;

public class IndentationInfo(IndentationType type, int size, int level = 0)
{
    public IndentationType Type { get; set; } = type;
    public int Size { get; set; } = size;
    public int Level { get; set; } = level;

    /// <summary>
    /// Gets the actual indentation string for this level
    /// </summary>
    public string GetIndentString()
    {
        return Type switch
        {
            IndentationType.Tabs => new string('\t', Level),
            IndentationType.Spaces => new string(' ', Level * Size),
            _ => string.Empty
        };
    }
    
    /// <summary>
    /// Detects indentation from a line of text
    /// </summary>
    public static IndentationInfo DetectFromLine(string line)
    {
        if (string.IsNullOrEmpty(line))
            return new IndentationInfo(IndentationType.Spaces, 4);
            
        var spaces = 0;
        var tabs = 0;
        
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == ' ')
                spaces++;
            else if (line[i] == '\t')
                tabs++;
            else
                break; // Hit non-whitespace
        }
        
        if (tabs > 0 && spaces > 0)
            return new IndentationInfo(IndentationType.Mixed, spaces, tabs);
        if (tabs > 0)
            return new IndentationInfo(IndentationType.Tabs, 1, tabs);
        if (spaces > 0)
        {
            // Try to detect common indentation sizes (2, 4, 8)
            var size = DetectSpaceSize(spaces);
            var level = spaces / size;
            return new IndentationInfo(IndentationType.Spaces, size, level);
        }

        return new IndentationInfo(IndentationType.Spaces, 4);
    }
    
    /// <summary>
    /// Creates indentation info for a specific level using the same style
    /// </summary>
    public IndentationInfo AtLevel(int newLevel)
    {
        return new IndentationInfo(Type, Size, newLevel);
    }
    
    private static int DetectSpaceSize(int totalSpaces)
    {
        // Common indentation sizes
        int[] commonSizes = [2, 4, 8];
        
        foreach (var size in commonSizes)
        {
            if (totalSpaces % size == 0)
                return size;
        }
        
        // Default to 4 if no common pattern found
        return 4;
    }
    
    public override string ToString()
    {
        return Type switch
        {
            IndentationType.Tabs => $"{Level} tabs",
            IndentationType.Spaces => $"{Level * Size} spaces ({Level} levels of {Size})",
            IndentationType.Mixed => $"Mixed: {Level} tabs + {Size} spaces",
            _ => "No indentation"
        };
    }
}