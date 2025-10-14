namespace DocumentServer.Services.FormatSpecific.Word.Models;

public class WordSection
{
    public string Title { get; set; } = "";
    public int Level { get; set; }
    public string Content { get; set; } = "";
    public List<WordSection> SubSections { get; set; } = [];
}
