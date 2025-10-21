namespace PlaywrightServerMcp.Models;

public class MockRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UrlPattern { get; set; } = "";
    public string Method { get; set; } = "GET";
    public string ResponseBody { get; set; } = "";
    public int StatusCode { get; set; } = 200;
    public Dictionary<string, string> Headers { get; set; } = new();
    public TimeSpan? Delay { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int TimesUsed { get; set; }
}