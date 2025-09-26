namespace DesktopDriver.Services.DocumentSearching.Models;

public class IndexMemoryStatus
{
    public string IndexName { get; set; } = "";
    public bool IsDiscovered { get; init; }
    public bool IsLoadedInMemory { get; init; }
    public double EstimatedMemoryUsageMb { get; set; }
    
    public string Status => IsLoadedInMemory ? "Loaded" : IsDiscovered ? "Discoverable" : "Unknown";
}