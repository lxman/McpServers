namespace McpCodeEditor.Models.Options;

public class GenerateConstructorOptions
{
    public List<string> Fields { get; set; } = [];
    public bool IncludeAllFields { get; set; } = true;
    public bool InitializeProperties { get; set; } = true;
    public string AccessModifier { get; set; } = "public";
    public bool AddNullChecks { get; set; } = true;
}
