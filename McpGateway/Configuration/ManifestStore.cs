using System.Text.Json;

namespace McpGateway.Configuration;

/// <summary>
/// servers.json is the source of truth for which version is active. Deliberately a file the
/// gateway rewrites rather than a directory junction: rollback is one field, and Windows never has
/// to retarget a path with open handles underneath it.
/// </summary>
public sealed class ManifestStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _path;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private volatile Dictionary<string, ServerEntry> _entries;

    private ManifestStore(string path, Dictionary<string, ServerEntry> entries)
    {
        _path = path;
        _entries = entries;
    }

    public IReadOnlyDictionary<string, ServerEntry> Entries => _entries;

    public static ManifestStore Load(string path)
    {
        Dictionary<string, ServerEntry> entries =
            JsonSerializer.Deserialize<Dictionary<string, ServerEntry>>(
                File.ReadAllText(path), Options)
            ?? throw new InvalidOperationException($"Manifest at {path} deserialized to null.");

        return new ManifestStore(path, new Dictionary<string, ServerEntry>(
            entries, StringComparer.OrdinalIgnoreCase));
    }

    public bool TryGet(string name, out ServerEntry? entry) => _entries.TryGetValue(name, out entry);

    public async Task SetActiveVersionAsync(
        string name, string version, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            if (!_entries.TryGetValue(name, out ServerEntry? entry))
            {
                throw new KeyNotFoundException($"No server named '{name}' in the manifest.");
            }

            var updated = new Dictionary<string, ServerEntry>(
                _entries, StringComparer.OrdinalIgnoreCase)
            {
                [name] = entry with { ActiveVersion = version }
            };

            string temp = _path + ".tmp";
            await File.WriteAllTextAsync(
                temp, JsonSerializer.Serialize(updated, Options), cancellationToken);
            File.Move(temp, _path, overwrite: true);

            _entries = updated;
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
