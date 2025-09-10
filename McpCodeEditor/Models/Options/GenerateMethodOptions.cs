namespace McpCodeEditor.Models.Options;

public class GenerateMethodOptions
{
    public string MethodType { get; set; } = string.Empty; // "Equals", "GetHashCode", "ToString"
    public List<string> IncludedMembers { get; set; } = [];
    public bool IncludeAllFields { get; set; } = true;
    public bool IncludeAllProperties { get; set; } = true;
    public string AccessModifier { get; set; } = "public";
}
