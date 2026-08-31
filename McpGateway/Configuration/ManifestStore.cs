using System.Text.Json;

namespace McpGateway.Configuration;

/// <summary>
/// Merges the two halves of a server's configuration: the static half in the git-tracked
/// servers.json, and the runtime half -- which version is active -- in a state file under
/// %LOCALAPPDATA%. The split is what stops a deploy dirtying the working tree, and stops a
/// git checkout quietly reverting the active version of a live server.
/// <para>
/// Still a file rather than a directory junction, for the reason it always was: rollback is one
/// field, and Windows never has to retarget a path with open handles underneath it.
/// </para>
/// </summary>
public sealed class ManifestStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _statePath;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private volatile Dictionary<string, ServerEntry> _entries;

    /// <summary>
    /// Any casing a caller might use, mapped to the manifest's own spelling. Built once: activating
    /// a version replaces entry VALUES, never the key set.
    /// </summary>
    private readonly Dictionary<string, string> _canonicalNames;

    private ManifestStore(string statePath, Dictionary<string, ServerEntry> entries)
    {
        _statePath = statePath;
        _entries = entries;
        _canonicalNames = entries.Keys.ToDictionary(
            key => key, key => key, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyDictionary<string, ServerEntry> Entries => _entries;

    public static ManifestStore Load(string manifestPath, string statePath)
    {
        Dictionary<string, ServerEntry> entries =
            JsonSerializer.Deserialize<Dictionary<string, ServerEntry>>(
                File.ReadAllText(manifestPath), Options)
            ?? throw new InvalidOperationException($"Manifest at {manifestPath} deserialized to null.");

        var merged = new Dictionary<string, ServerEntry>(entries, StringComparer.OrdinalIgnoreCase);

        foreach ((string name, string version) in ReadState(statePath).ActiveVersions)
        {
            // A recorded version for a server that servers.json no longer lists is simply dropped.
            // Removing a server from the manifest is a deliberate act; resurrecting it from runtime
            // state would undo it.
            if (merged.TryGetValue(name, out ServerEntry? entry))
            {
                merged[name] = entry with { ActiveVersion = version };
            }
        }

        return new ManifestStore(statePath, merged);
    }

    public bool TryGet(string name, out ServerEntry? entry) => TryGet(name, out entry, out _);

    /// <summary>
    /// Looks a server up by any casing and hands back the manifest's own spelling of its name.
    /// <para>
    /// Callers that turn a name into a <c>BackendKey</c> must use the canonical one. Lookup here is
    /// case-insensitive but BackendKey's equality is not, so routing a mis-cased URL straight
    /// through resolves the right entry and then lands in a SECOND pool slot: a second live backend
    /// for a server declared overlapAllowed: false, invisible to an activation that filters on the
    /// canonical name, and outside the prune keep-set -- which is what lets prune delete a
    /// directory a live backend is running from.
    /// </para>
    /// </summary>
    public bool TryGet(string name, out ServerEntry? entry, out string canonicalName)
    {
        if (_entries.TryGetValue(name, out entry))
        {
            canonicalName = _canonicalNames.TryGetValue(name, out string? canonical) ? canonical : name;
            return true;
        }

        canonicalName = name;
        return false;
    }

    /// <summary>
    /// Records the active version. Writes the runtime state file only -- servers.json is static
    /// config and is never rewritten.
    /// </summary>
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

            var state = new GatewayState
            {
                ActiveVersions = updated
                    .Where(pair => pair.Value.ActiveVersion is not null)
                    .ToDictionary(
                        pair => pair.Key,
                        pair => pair.Value.ActiveVersion!,
                        StringComparer.OrdinalIgnoreCase)
            };

            string? directory = Path.GetDirectoryName(_statePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            string temp = _statePath + ".tmp";
            await File.WriteAllTextAsync(
                temp, JsonSerializer.Serialize(state, Options), cancellationToken);
            File.Move(temp, _statePath, overwrite: true);

            _entries = updated;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private static GatewayState ReadState(string statePath)
    {
        // Absent is the ordinary first-run case: nothing has been deployed yet, so every server is
        // "not yet deployed" and says so when something tries to start it.
        if (!File.Exists(statePath)) return new GatewayState();

        try
        {
            return JsonSerializer.Deserialize<GatewayState>(File.ReadAllText(statePath), Options)
                   ?? new GatewayState();
        }
        catch (JsonException ex)
        {
            // Loudly, rather than silently treating every server as undeployed. The file is written
            // atomically, so corruption means something outside the gateway edited it, and quietly
            // discarding a hand-edit would hide the mistake behind a fleet that is merely "down".
            throw new InvalidOperationException(
                $"Runtime state at {statePath} is not valid JSON. Fix or delete it; deleting it " +
                "makes every server undeployed until it is activated again.", ex);
        }
    }
}
