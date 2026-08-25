using System.Collections.Concurrent;

namespace CodeAssist.Core.Services;

/// <summary>
/// Prevents a value computed against an old revision from repopulating an invalidated cache.
/// </summary>
internal sealed class VersionedCache<T> where T : class
{
    private readonly ConcurrentDictionary<string, T> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, long> _versions = new(StringComparer.OrdinalIgnoreCase);

    public T? Get(string key) => _values.GetValueOrDefault(key);

    public bool TryGet(string key, out T value) => _values.TryGetValue(key, out value!);

    public long CaptureVersion(string key) => _versions.GetValueOrDefault(key);

    public bool TryStore(string key, long expectedVersion, T value)
    {
        if (CaptureVersion(key) != expectedVersion) return false;

        _values[key] = value;

        if (CaptureVersion(key) == expectedVersion) return true;

        if (_values.TryGetValue(key, out T? current) && ReferenceEquals(current, value))
        {
            _values.TryRemove(key, out _);
        }

        return false;
    }

    public void Invalidate(string key)
    {
        _versions.AddOrUpdate(key, 1, (_, version) => unchecked(version + 1));
        _values.TryRemove(key, out _);
    }
}
