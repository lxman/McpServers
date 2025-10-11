namespace OfficeReader.Models.Word;

public class WordParagraph
{
    public string Text { get; set; } = "";
    public string Style { get; set; } = "";
    public bool IsBold { get; set; }
    public bool IsItalic { get; set; }
    public bool IsUnderlined { get; set; }
}