using McpCodeEditor.Interfaces;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using McpCodeEditor.Models;
using McpCodeEditor.Models.Options;

namespace McpCodeEditor.Services.CodeGeneration;

public class ConstructorGenerator(
    CodeEditorConfigurationService config,
    IBackupService backupService,
    IChangeTrackingService changeTracking)
{
    /// <summary>
    /// Generate a constructor from class fields and properties - FIXED VERSION with Path Resolution
    /// </summary>
    public async Task<CodeGenerationResult> GenerateConstructorAsync(
        string filePath,
        string className,
        GenerateConstructorOptions options,
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // FIXED: Resolve relative paths to absolute paths
            var resolvedFilePath = ValidateAndResolvePath(filePath);

            if (!File.Exists(resolvedFilePath))
            {
                return new CodeGenerationResult
                {
                    Success = false,
                    Error = $"File not found: {filePath}"
                };
            }

            var sourceCode = await File.ReadAllTextAsync(resolvedFilePath, cancellationToken);
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, cancellationToken: cancellationToken);

            if (await syntaxTree.GetRootAsync(cancellationToken) is not CompilationUnitSyntax root)
            {
                return new CodeGenerationResult
                {
                    Success = false,
                    Error = "Failed to parse C# file"
                };
            }

            // Find the target class
            var targetClass = root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c => c.Identifier.ValueText == className);

            if (targetClass == null)
            {
                return new CodeGenerationResult
                {
                    Success = false,
                    Error = $"Class '{className}' not found in file"
                };
            }

            // Get fields and properties for constructor
            var fields = targetClass.DescendantNodes()
                .OfType<FieldDeclarationSyntax>()
                .Where(f => !f.Modifiers.Any(SyntaxKind.StaticKeyword))
                .SelectMany(f => f.Declaration.Variables)
                .Select(v => new { Name = v.Identifier.ValueText, Type = GetFieldType(v) })
                .ToList();

            var properties = targetClass.DescendantNodes()
                .OfType<PropertyDeclarationSyntax>()
                .Where(p => !p.Modifiers.Any(SyntaxKind.StaticKeyword) && HasSetter(p))
                .Select(p => new { Name = p.Identifier.ValueText, Type = p.Type.ToString() })
                .ToList();

            // Determine what to include in constructor
            var membersToInclude = new List<(string Name, string Type)>();

            if (options.IncludeAllFields && fields.Count != 0)
            {
                membersToInclude.AddRange(fields.Select(f => (f.Name, f.Type)));
            }
            else if (options.Fields.Count != 0)
            {
                membersToInclude.AddRange(fields.Where(f => options.Fields.Contains(f.Name))
                    .Select(f => (f.Name, f.Type)));
            }

            if (options.InitializeProperties && properties.Count != 0)
            {
                membersToInclude.AddRange(properties.Select(p => (p.Name, p.Type)));
            }

            if (membersToInclude.Count == 0)
            {
                return new CodeGenerationResult
                {
                    Success = false,
                    Error = "No fields or properties found to generate constructor"
                };
            }

            // Generate constructor code
            var constructorCode = GenerateConstructorCode(className, membersToInclude, options);

            // Check if constructor already exists
            var hasExistingConstructor = targetClass.DescendantNodes()
                .OfType<ConstructorDeclarationSyntax>()
                .Any(c => c.ParameterList.Parameters.Count == membersToInclude.Count);

            if (hasExistingConstructor)
            {
                return new CodeGenerationResult
                {
                    Success = false,
                    Error = "A constructor with similar parameters already exists"
                };
            }

            // Insert constructor into class
            var modifiedContent = InsertConstructorIntoClass(sourceCode, targetClass, constructorCode);

            // Create backup if not preview
            string? backupId = null;
            if (!previewOnly)
            {
                backupId = await backupService.CreateBackupAsync(
                    Path.GetDirectoryName(resolvedFilePath)!,
                    $"generate_constructor_{className}");
            }

            // Apply changes if not preview
            if (!previewOnly)
            {
                await File.WriteAllTextAsync(resolvedFilePath, modifiedContent, cancellationToken);

                await changeTracking.TrackChangeAsync(
                    resolvedFilePath,
                    sourceCode,
                    modifiedContent,
                    $"Generate constructor for class '{className}'",
                    backupId);
            }

            return new CodeGenerationResult
            {
                Success = true,
                Message = previewOnly
                    ? $"Preview: Would generate constructor for '{className}' with {membersToInclude.Count} parameters"
                    : $"Successfully generated constructor for '{className}' with {membersToInclude.Count} parameters",
                GeneratedCode = constructorCode,
                ModifiedFileContent = modifiedContent,
                FilePath = resolvedFilePath,
                Metadata = new Dictionary<string, object>
                {
                    ["className"] = className,
                    ["parameterCount"] = membersToInclude.Count,
                    ["backupId"] = backupId ?? "",
                    ["membersIncluded"] = membersToInclude.Select(m => m.Name).ToArray()
                }
            };
        }
        catch (Exception ex)
        {
            return new CodeGenerationResult
            {
                Success = false,
                Error = $"Constructor generation failed: {ex.Message}"
            };
        }
    }

    #region Helper Methods

    private static string GetFieldType(VariableDeclaratorSyntax variable)
    {
        var field = variable.FirstAncestorOrSelf<FieldDeclarationSyntax>();
        return field?.Declaration?.Type?.ToString() ?? "object";
    }

    private static bool HasSetter(PropertyDeclarationSyntax property)
    {
        return property.AccessorList?.Accessors
            .Any(a => a.Keyword.IsKind(SyntaxKind.SetKeyword)) ?? false;
    }

    private static string GenerateConstructorCode(
        string className,
        List<(string Name, string Type)> members,
        GenerateConstructorOptions options)
    {
        var parameters = members.Select(m => $"{m.Type} {ToCamelCase(m.Name)}").ToList();
        var assignments = new List<string>();

        foreach (var member in members)
        {
            var paramName = ToCamelCase(member.Name);

            if (options.AddNullChecks && IsReferenceType(member.Type))
            {
                assignments.Add($"        this.{member.Name} = {paramName} ?? throw new ArgumentNullException(nameof({paramName}));");
            }
            else
            {
                assignments.Add($"        this.{member.Name} = {paramName};");
            }
        }

        return $$"""

                     {{options.AccessModifier}} {{className}}({{string.Join(", ", parameters)}})
                     {
                 {{string.Join("\n", assignments)}}
                     }
                 """;
    }

    private static string InsertConstructorIntoClass(
        string sourceCode,
        ClassDeclarationSyntax targetClass,
        string constructorCode)
    {
        // Find a good insertion point (after fields but before methods)
        var lines = sourceCode.Split('\n');
        var insertionLine = FindInsertionPointForConstructor(lines, targetClass);

        var modifiedLines = lines.ToList();
        modifiedLines.Insert(insertionLine, constructorCode);

        return string.Join("\n", modifiedLines);
    }

    private static int FindInsertionPointForConstructor(string[] lines, ClassDeclarationSyntax targetClass)
    {
        // Simple heuristic: insert after last field or at beginning of class body
        var classStartLine = targetClass.GetLocation().GetLineSpan().StartLinePosition.Line;

        // Look for opening brace
        for (var i = classStartLine; i < lines.Length; i++)
        {
            if (lines[i].Contains("{"))
            {
                return i + 1;
            }
        }

        return classStartLine + 1;
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    private static bool IsReferenceType(string typeName)
    {
        // Simple heuristic for reference types
        var valueTypes = new[] { "int", "long", "short", "byte", "bool", "char", "float", "double", "decimal", "DateTime", "TimeSpan", "Guid" };
        return !valueTypes.Any(vt => typeName.StartsWith(vt));
    }

    /// <summary>
    /// FIXED: Added path resolution similar to FileOperationsService
    /// Converts relative paths to absolute paths and validates security
    /// </summary>
    private string ValidateAndResolvePath(string path)
    {
        // Convert to absolute path
        var fullPath = Path.IsPathRooted(path) ? path : Path.Combine(config.DefaultWorkspace, path);
        fullPath = Path.GetFullPath(fullPath);

        // Security check: ensure path is within workspace if restricted
        if (config.Security.RestrictToWorkspace)
        {
            var workspaceFullPath = Path.GetFullPath(config.DefaultWorkspace);
            if (!fullPath.StartsWith(workspaceFullPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException($"Access denied: Path outside workspace: {path}");
            }
        }

        // Check blocked paths
        foreach (var blockedPath in config.Security.BlockedPaths)
        {
            var blockedFullPath = Path.GetFullPath(blockedPath);
            if (fullPath.StartsWith(blockedFullPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException($"Access denied: Blocked path: {path}");
            }
        }

        return fullPath;
    }

    #endregion
}
