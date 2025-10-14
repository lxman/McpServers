// ReSharper disable InvalidXmlDocComment
namespace DesktopCommanderMcp.Services.AdvancedFileEditing.Models;

public class LineRange(int startLine, int endLine)
{
    public int StartLine { get; set; } = startLine;
    public int EndLine { get; set; } = endLine;

    /// <summary>
    /// Number of lines in this range (inclusive)
    /// </summary>
    public int LineCount => EndLine - StartLine + 1;
    
    /// <summary>
    /// Checks if this range is valid (start <= end, both positive)
    /// </summary>
    public bool IsValid => StartLine >= 1 && EndLine >= StartLine;
    
    /// <summary>
    /// Checks if the given line number is within this range (inclusive)
    /// </summary>
    public bool Contains(int lineNumber)
    {
        return lineNumber >= StartLine && lineNumber <= EndLine;
    }
    
    /// <summary>
    /// Checks if this range overlaps with another range
    /// </summary>
    public bool OverlapsWith(LineRange other)
    {
        return StartLine <= other.EndLine && EndLine >= other.StartLine;
    }
    
    /// <summary>
    /// Creates a single-line range
    /// </summary>
    public static LineRange Single(int lineNumber)
    {
        return new LineRange(lineNumber, lineNumber);
    }
    
    /// <summary>
    /// Validates that a line range is within the bounds of a file
    /// </summary>
    public bool IsWithinFile(int totalLines)
    {
        return IsValid && StartLine <= totalLines && EndLine <= totalLines;
    }
    
    public override string ToString()
    {
        return StartLine == EndLine ? $"Line {StartLine}" : $"Lines {StartLine}-{EndLine}";
    }
}