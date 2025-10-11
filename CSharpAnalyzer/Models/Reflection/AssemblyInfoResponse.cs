namespace CSharpAnalyzer.Models.Reflection;

public class AssemblyInfoResponse
{
    public bool Success { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Culture { get; set; } = string.Empty;
    public string PublicKeyToken { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string TargetFramework { get; set; } = string.Empty;
    public List<string> ReferencedAssemblies { get; set; } = [];
    public string? Error { get; set; }
}
