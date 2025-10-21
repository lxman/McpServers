namespace PlaywrightServerMcp.Models;

public class InterceptRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UrlPattern { get; set; } = "";
    public string Method { get; set; } = "*"; // * for all methods
    public string Action { get; set; } = "block"; // block, modify, log
    public string? ModifiedBody { get; set; }
    public Dictionary<string, string> ModifiedHeaders { get; set; } = new();
    public int? ModifiedStatusCode { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int TimesTriggered { get; set; }
}