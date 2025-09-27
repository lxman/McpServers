namespace OfficeMcp.Models.Word;

public class WordComment
{
    public string Author { get; set; } = "";
    public DateTime Date { get; set; }
    public string Text { get; set; } = "";
    public string ReferencedText { get; set; } = "";
}