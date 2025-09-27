namespace OfficeMcp.Models.Word;

public class WordSection
{
    public int SectionNumber { get; set; }
    public string Title { get; set; } = "";
    public int Level { get; set; }
    public string Content { get; set; } = "";
    public List<WordParagraph> Paragraphs { get; set; } = [];
}