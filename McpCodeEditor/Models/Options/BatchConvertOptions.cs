namespace McpCodeEditor.Models.Options;

public class BatchConvertOptions
{
    public string FromExtension { get; set; } = string.Empty;
    public string ToExtension { get; set; } = string.Empty;
    public bool DeleteOriginal { get; set; } = false;
    public bool CreateBackup { get; set; } = true;
    public Dictionary<string, object> ConversionSettings { get; set; } = new();
}
