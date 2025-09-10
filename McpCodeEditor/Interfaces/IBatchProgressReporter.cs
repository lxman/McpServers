namespace McpCodeEditor.Interfaces;

public interface IBatchProgressReporter
{
    void ReportProgress(int processed, int total, string? currentFile = null);
    void ReportFileCompleted(string filePath, bool success, string? error = null);
}
