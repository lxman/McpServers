namespace OfficeReader.Models;

public class OfficeMetadata
{
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Keywords { get; set; } = "";
    public string Comments { get; set; } = "";
    public string Company { get; set; } = "";
    public string Creator { get; set; } = "";
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }
    public string LastSavedBy { get; set; } = "";
    public string Version { get; set; } = "";
    public int PageCount { get; set; }
    public int WordCount { get; set; }
    public bool IsPasswordProtected { get; set; }
}