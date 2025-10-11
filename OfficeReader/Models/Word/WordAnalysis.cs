namespace OfficeReader.Models.Word;

public class WordAnalysis
{
    public int SectionCount { get; set; }
    public int TableCount { get; set; }
    public int CommentCount { get; set; }
    public int StyleCount { get; set; }
    public int WordCount { get; set; }
    public int CharacterCount { get; set; }
    public List<string> HeadingStructure { get; set; } = [];
}