namespace CSharpAnalyzerMcp.Models.Reflection;

public class LoadAssemblyRequest
{
    public string AssemblyPath { get; set; } = string.Empty;
    public List<string> SearchPaths { get; set; } = [];
    public bool IncludeFrameworkAssemblies { get; set; } = true;
}
