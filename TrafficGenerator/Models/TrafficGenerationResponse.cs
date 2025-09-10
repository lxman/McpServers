namespace TrafficGenerator.Models;

public class TrafficGenerationResponse
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public string Status { get; set; } = "started";
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public string TrafficType { get; set; } = string.Empty;
    public int PacketsGenerated { get; set; }
    public long BytesGenerated { get; set; }
    public string[] Warnings { get; set; } = [];
    public Dictionary<string, object> Details { get; set; } = new();
}