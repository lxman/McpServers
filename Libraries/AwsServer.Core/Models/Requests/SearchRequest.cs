namespace AwsServer.Core.Models.Requests;

public class SearchRequest
{
    public string SearchText { get; set; } = string.Empty;
    public int Minutes { get; set; } = 60;
    public int MaxLogGroups { get; set; } = 5;
}