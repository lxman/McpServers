namespace AzureServer.Services.DevOps.Models;

public class BuildStepLogDto
{
    public string StepName { get; set; } = string.Empty;
    public string StepId { get; set; } = string.Empty;
    public DateTime? StartTime { get; set; }
    public DateTime? FinishTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string LogContent { get; set; } = string.Empty;
    public List<string> ErrorMessages { get; set; } = [];
    public List<string> WarningMessages { get; set; } = [];
}