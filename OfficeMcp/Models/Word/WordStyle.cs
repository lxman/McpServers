namespace OfficeMcp.Models.Word;

public class WordStyle
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string FontFamily { get; set; } = "";
    public int FontSize { get; set; }
    public bool IsBold { get; set; }
    public bool IsItalic { get; set; }
}