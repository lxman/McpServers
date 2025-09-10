using McpCodeEditor.Interfaces;

namespace McpCodeEditor.Models;

public class BatchProgressReporter : IBatchProgressReporter
{
    public event Action<int, int, string?>? ProgressChanged;  // Made nullable to fix CS8618
    public event Action<string, bool, string?>? FileCompleted;  // Made nullable to fix CS8618

    public void ReportProgress(int processed, int total, string? currentFile = null)
    {
        ProgressChanged?.Invoke(processed, total, currentFile);
    }

    public void ReportFileCompleted(string filePath, bool success, string? error = null)
    {
        FileCompleted?.Invoke(filePath, success, error);
    }
}
