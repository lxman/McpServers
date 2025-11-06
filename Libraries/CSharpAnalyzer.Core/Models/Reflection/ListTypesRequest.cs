namespace CSharpAnalyzer.Core.Models.Reflection;

public class ListTypesRequest
{
    public string AssemblyPath { get; set; } = string.Empty;
    public bool PublicOnly { get; set; } = true;
    public string? NamespaceFilter { get; set; }
    public string? TypeKindFilter { get; set; } // class, interface, enum, struct
}
