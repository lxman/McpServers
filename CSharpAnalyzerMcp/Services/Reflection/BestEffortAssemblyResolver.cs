using System.Reflection;

namespace CSharpAnalyzerMcp.Services.Reflection;

/// <summary>
/// Assembly resolver that attempts to resolve dependencies from multiple search locations.
/// Continues with best-effort even if some dependencies cannot be resolved.
/// </summary>
public class BestEffortAssemblyResolver(IEnumerable<string> searchPaths) : MetadataAssemblyResolver
{
    private readonly List<string> _searchPaths = searchPaths.ToList();
    private readonly HashSet<string> _unresolvedAssemblies = new(StringComparer.OrdinalIgnoreCase);
    
    public IReadOnlyCollection<string> UnresolvedAssemblies => _unresolvedAssemblies;

    public override Assembly? Resolve(MetadataLoadContext context, AssemblyName assemblyName)
    {
        // Try to find the assembly in search paths
        foreach (string searchPath in _searchPaths)
        {
            // Try with .dll extension
            string dllPath = Path.Combine(searchPath, $"{assemblyName.Name}.dll");
            if (File.Exists(dllPath))
            {
                try
                {
                    return context.LoadFromAssemblyPath(dllPath);
                }
                catch
                {
                    // Continue trying other paths
                }
            }

            // Try with .exe extension
            string exePath = Path.Combine(searchPath, $"{assemblyName.Name}.exe");
            if (!File.Exists(exePath)) continue;
            try
            {
                return context.LoadFromAssemblyPath(exePath);
            }
            catch
            {
                // Continue trying other paths
            }
        }

        // Could not resolve - track it but don't fail
        _unresolvedAssemblies.Add(assemblyName.Name ?? "Unknown");
        return null;
    }
}
