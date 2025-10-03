namespace DesktopDriver.Exceptions;

/// <summary>
/// Exception thrown when a file version conflict is detected during optimistic locking.
/// </summary>
public class FileConflictException(string message, string expectedVersion, string currentVersion, string filePath)
    : Exception(message)
{
    public string ExpectedVersion { get; } = expectedVersion;
    public string CurrentVersion { get; } = currentVersion;
    public string FilePath { get; } = filePath;
}
