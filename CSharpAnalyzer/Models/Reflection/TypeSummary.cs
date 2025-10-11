namespace CSharpAnalyzer.Models.Reflection;

public class TypeSummary
{
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string TypeKind { get; set; } = string.Empty; // Class, Interface, Enum, Struct, Delegate
    public bool IsPublic { get; set; }
    public bool IsAbstract { get; set; }
    public bool IsSealed { get; set; }
    public bool IsGeneric { get; set; }
    public string? BaseType { get; set; }
    public List<string> Interfaces { get; set; } = [];
}
