namespace OfficeMcp.Models.Word;

public class WordComment
{
    public string Id { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Text { get; set; } = string.Empty;
    public string ReferencedText { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}