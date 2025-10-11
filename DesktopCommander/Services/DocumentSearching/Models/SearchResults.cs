namespace DesktopCommander.Services.DocumentSearching.Models;

public class SearchResults
{
    public string Query { get; set; } = string.Empty;
    public List<SearchResult> Results { get; set; } = [];
    public int TotalHits { get; set; }
    public double SearchTimeMs { get; set; }
    public Dictionary<string, int> FileTypeCounts { get; set; } = new();
    public Dictionary<string, int> DirectoryCounts { get; set; } = new();
}
