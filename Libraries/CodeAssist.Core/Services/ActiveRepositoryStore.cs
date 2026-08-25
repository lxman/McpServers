using CodeAssist.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeAssist.Core.Services;

/// <summary>
/// Persists the repository whose watcher should be restored when the MCP host restarts.
/// </summary>
public sealed class ActiveRepositoryStore(
    IOptions<CodeAssistOptions> options,
    ILogger<ActiveRepositoryStore> logger)
{
    private readonly object _gate = new();
    private readonly string _path = Path.Combine(options.Value.IndexStateDirectory, "active-repository");

    public string? TryLoad()
    {
        lock (_gate)
        {
            try
            {
                if (!File.Exists(_path)) return null;

                string repositoryName = File.ReadAllText(_path).Trim();
                return string.IsNullOrWhiteSpace(repositoryName) ? null : repositoryName;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not read active repository state from {Path}", _path);
                return null;
            }
        }
    }

    public bool TrySave(string repositoryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryName);

        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                if (File.Exists(_path)
                    && string.Equals(File.ReadAllText(_path).Trim(), repositoryName.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                string tempPath = _path + ".tmp";
                File.WriteAllText(tempPath, repositoryName.Trim());
                File.Move(tempPath, _path, overwrite: true);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not persist active repository {Repository} to {Path}",
                    repositoryName, _path);
                return false;
            }
        }
    }

    public bool TryClear()
    {
        lock (_gate)
        {
            try
            {
                File.Delete(_path);
                File.Delete(_path + ".tmp");
                return true;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not clear active repository state at {Path}", _path);
                return false;
            }
        }
    }
}
