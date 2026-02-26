using System.Text.RegularExpressions;

namespace CodeAssist.Core.Chunking;

/// <summary>
/// Parses DI registration patterns from Program.cs / ServiceCollectionExtensions.cs
/// to extract interface-to-implementation mappings.
/// </summary>
internal static partial class DiRegistrationParser
{
    /// <summary>
    /// Extract interface→implementation mappings from source code containing DI registrations.
    /// Recognizes AddSingleton, AddScoped, AddTransient, AddHostedService, and common
    /// custom helpers like AddScopedWithLogger, AddSingletonWithFactory, etc.
    /// </summary>
    public static Dictionary<string, string> ExtractServiceMappings(string sourceContent)
    {
        var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Match generic two-type-arg registrations:
        //   AddSingleton<IFoo, Foo>()
        //   AddScoped<IFoo, Foo>()
        //   AddTransient<IFoo, Foo>()
        //   AddScopedWithLogger<IFoo, Foo>()
        //   AddSingletonWithFactory<IFoo, Foo, FooFactory>()
        //   etc.
        foreach (Match match in TwoTypeArgPattern().Matches(sourceContent))
        {
            string iface = NormalizeTypeName(match.Groups[1].Value);
            string impl = NormalizeTypeName(match.Groups[2].Value);
            mappings.TryAdd(iface, impl);
        }

        // Match AddHostedService<Foo>() — maps to IHostedService
        foreach (Match match in HostedServicePattern().Matches(sourceContent))
        {
            string impl = NormalizeTypeName(match.Groups[1].Value);
            mappings.TryAdd("IHostedService", impl);
        }

        // Match AddAWSService<IFoo>() — self-mapping (interface registered as itself)
        // These don't provide impl info, so skip them.

        return mappings;
    }

    /// <summary>
    /// Check whether a file path looks like a DI registration file.
    /// </summary>
    public static bool IsDiRegistrationFile(string relativePath)
    {
        string fileName = Path.GetFileName(relativePath);
        return fileName.Equals("Program.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("Startup.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith("ServiceCollectionExtensions.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeTypeName(string typeName)
    {
        // Strip generic args for mapping purposes:
        // IOptions<CodeAssistOptions> → IOptions
        // ILogger<Foo> → ILogger
        int angleBracket = typeName.IndexOf('<');
        return angleBracket >= 0 ? typeName[..angleBracket].Trim() : typeName.Trim();
    }

    // Pattern: .Add{Singleton|Scoped|Transient}[WithLogger|WithFactory...]<IFoo, Foo>
    // Also matches custom helpers with 2+ type args where first is the interface.
    [GeneratedRegex(
        @"\.Add(?:Singleton|Scoped|Transient)\w*<\s*([A-Za-z_][\w.]*(?:<[^>]*>)?)\s*,\s*([A-Za-z_][\w.]*(?:<[^>]*>)?)\s*[,>]",
        RegexOptions.Compiled)]
    private static partial Regex TwoTypeArgPattern();

    // Pattern: .AddHostedService<Foo>()
    [GeneratedRegex(
        @"\.AddHostedService<\s*([A-Za-z_][\w.]*(?:<[^>]*>)?)\s*>",
        RegexOptions.Compiled)]
    private static partial Regex HostedServicePattern();
}
