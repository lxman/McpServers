using System.Reflection;
using CSharpAnalyzer.Models.Reflection;

namespace CSharpAnalyzer.Services.Reflection;

/// <summary>
/// Service for loading and managing assemblies using MetadataLoadContext for safe inspection.
/// </summary>
public class AssemblyLoaderService : IDisposable
{
    private readonly Dictionary<string, (MetadataLoadContext Context, Assembly Assembly, BestEffortAssemblyResolver Resolver)> _loadedAssemblies = new();

    public LoadAssemblyResponse LoadAssembly(string assemblyPath, List<string>? searchPaths = null, bool includeFramework = true)
    {
        var response = new LoadAssemblyResponse { Success = false };

        try
        {
            if (!File.Exists(assemblyPath))
            {
                response.Error = $"Assembly file not found: {assemblyPath}";
                return response;
            }

            // If already loaded, return cached info
            if (_loadedAssemblies.TryGetValue(assemblyPath, out (MetadataLoadContext Context, Assembly Assembly, BestEffortAssemblyResolver Resolver) cached))
            {
                response.Success = true;
                response.AssemblyName = cached.Assembly.GetName().Name ?? string.Empty;
                response.FullName = cached.Assembly.FullName ?? string.Empty;
                response.Location = assemblyPath;
                response.UnresolvedDependencies = cached.Resolver.UnresolvedAssemblies.ToList();
                return response;
            }

            // Build search paths
            var paths = new List<string>();

            // Add assembly's own directory
            string? assemblyDir = Path.GetDirectoryName(assemblyPath);
            if (!string.IsNullOrEmpty(assemblyDir))
            {
                paths.Add(assemblyDir);
            }

            // Add framework assemblies if requested
            if (includeFramework)
            {
                string runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location) ?? string.Empty;
                if (!string.IsNullOrEmpty(runtimeDir))
                {
                    paths.Add(runtimeDir);
                }
            }

            // Add user-provided search paths
            if (searchPaths != null)
            {
                paths.AddRange(searchPaths.Where(Directory.Exists));
            }

            // Create resolver and context
            var resolver = new BestEffortAssemblyResolver(paths);
            var context = new MetadataLoadContext(resolver);

            // Load the assembly
            Assembly assembly = context.LoadFromAssemblyPath(assemblyPath);

            // Cache it
            _loadedAssemblies[assemblyPath] = (context, assembly, resolver);

            // Build response
            response.Success = true;
            response.AssemblyName = assembly.GetName().Name ?? string.Empty;
            response.FullName = assembly.FullName ?? string.Empty;
            response.Location = assemblyPath;
            response.UnresolvedDependencies = resolver.UnresolvedAssemblies.ToList();
        }
        catch (Exception ex)
        {
            response.Error = $"Failed to load assembly: {ex.Message}";
        }

        return response;
    }

    public Assembly? GetLoadedAssembly(string assemblyPath)
    {
        return _loadedAssemblies.TryGetValue(assemblyPath, out (MetadataLoadContext Context, Assembly Assembly, BestEffortAssemblyResolver Resolver) entry) ? entry.Assembly : null;
    }

    public MetadataLoadContext? GetContext(string assemblyPath)
    {
        return _loadedAssemblies.TryGetValue(assemblyPath, out (MetadataLoadContext Context, Assembly Assembly, BestEffortAssemblyResolver Resolver) entry) ? entry.Context : null;
    }

    public void UnloadAssembly(string assemblyPath)
    {
        if (!_loadedAssemblies.TryGetValue(assemblyPath, out (MetadataLoadContext Context, Assembly Assembly, BestEffortAssemblyResolver Resolver) entry)) return;
        entry.Context.Dispose();
        _loadedAssemblies.Remove(assemblyPath);
    }

    public void Dispose()
    {
        foreach ((MetadataLoadContext Context, Assembly Assembly, BestEffortAssemblyResolver Resolver) entry in _loadedAssemblies.Values)
        {
            entry.Context.Dispose();
        }
        _loadedAssemblies.Clear();
    }
}
