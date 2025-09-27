namespace OfficeMcp.Models.Word;

public class WordContent
{
    public List<WordSection> Sections { get; set; } = [];
    public List<WordTable> Tables { get; set; } = [];
    public List<WordComment> Comments { get; set; } = [];
    public List<WordStyle> Styles { get; set; } = [];
    public string PlainText { get; set; } = "";
}