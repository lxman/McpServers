namespace CSharpAnalyzer.Core.Models.Reflection;

public class LoadAssemblyResponse
{
    public bool Success { get; set; }
    public string AssemblyName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public List<string> UnresolvedDependencies { get; set; } = [];
    public string? Error { get; set; }
}
