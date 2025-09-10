using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using McpCodeEditor.Models;
using McpCodeEditor.Models.Options;
using McpCodeEditor.Services.CodeGeneration;

namespace McpCodeEditor.Services;

public class CodeGenerationService(
    CodeEditorConfigurationService config,
    ConstructorGenerator constructorGenerator)
{
    /// <summary>
    /// Generate a constructor from class fields and properties
    /// </summary>
    public async Task<CodeGenerationResult> GenerateConstructorAsync(
        string filePath,
        string className,
        GenerateConstructorOptions options,
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        return await constructorGenerator.GenerateConstructorAsync(
            filePath, className, options, previewOnly, cancellationToken);
    }

    /// <summary>
    /// Generate Equals method for a class - FIXED VERSION with Path Resolution
    /// </summary>
    public async Task<CodeGenerationResult> GenerateEqualsMethodAsync(
        string filePath,
        string className,
        GenerateMethodOptions options,
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // FIXED: Resolve relative paths to absolute paths
            string resolvedFilePath = ValidateAndResolvePath(filePath);

            if (!File.Exists(resolvedFilePath))
            {
                return new CodeGenerationResult
                {
                    Success = false,
                    Error = $"File not found: {filePath}"
                };
            }

            string sourceCode = await File.ReadAllTextAsync(resolvedFilePath, cancellationToken);
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, cancellationToken: cancellationToken);

            if (await syntaxTree.GetRootAsync(cancellationToken) is not CompilationUnitSyntax root)
            {
                return new CodeGenerationResult
                {
                    Success = false,
                    Error = "Failed to parse C# file"
                };
            }

            // Find the target class
            ClassDeclarationSyntax? targetClass = root.DescendantNodes()
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

            // Get members to include in Equals
            List<(string Name, string Type)> members = GetMembersForComparison(targetClass, options);

            if (members.Count == 0)
            {
                return new CodeGenerationResult
                {
                    Success = false,
                    Error = "No fields or properties found to generate Equals method"
                };
            }

            // Generate Equals method code
            string equalsCode = GenerateEqualsMethodCode(className, members, options);

            // Check if Equals already exists
            bool hasExistingEquals = targetClass.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Any(m => m.Identifier.ValueText == "Equals" &&
                         m.ParameterList.Parameters.Count == 1 &&
                         m.ParameterList.Parameters[0].Type?.ToString() == "object");

            if (hasExistingEquals)
            {
                return new CodeGenerationResult
                {
                    Success = false,
                    Error = "Equals method already exists in the class"
                };
            }

            // Insert method into class
            string modifiedContent = InsertMethodIntoClass(sourceCode, targetClass, equalsCode);

            if (!previewOnly)
            {
                await File.WriteAllTextAsync(resolvedFilePath, modifiedContent, cancellationToken);
            }

            return new CodeGenerationResult
            {
                Success = true,
                Message = previewOnly
                    ? $"Preview: Would generate Equals method for '{className}' with {members.Count} members"
                    : $"Successfully generated Equals method for '{className}' with {members.Count} members",
                GeneratedCode = equalsCode,
                ModifiedFileContent = modifiedContent,
                FilePath = resolvedFilePath,
                Metadata = new Dictionary<string, object>
                {
                    ["className"] = className,
                    ["memberCount"] = members.Count,
                    ["membersIncluded"] = members.Select(m => m.Name).ToArray()
                }
            };
        }
        catch (Exception ex)
        {
            return new CodeGenerationResult
            {
                Success = false,
                Error = $"Equals method generation failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Generate GetHashCode method for a class - FIXED VERSION with Path Resolution
    /// </summary>
    public async Task<CodeGenerationResult> GenerateGetHashCodeMethodAsync(
        string filePath,
        string className,
        GenerateMethodOptions options,
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // FIXED: Resolve relative paths to absolute paths
            string resolvedFilePath = ValidateAndResolvePath(filePath);

            if (!File.Exists(resolvedFilePath))
            {
                return new CodeGenerationResult
                {
                    Success = false,
                    Error = $"File not found: {filePath}"
                };
            }

            string sourceCode = await File.ReadAllTextAsync(resolvedFilePath, cancellationToken);
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, cancellationToken: cancellationToken);

            if (await syntaxTree.GetRootAsync(cancellationToken) is not CompilationUnitSyntax root)
            {
                return new CodeGenerationResult
                {
                    Success = false,
                    Error = "Failed to parse C# file"
                };
            }

            // Find the target class
            ClassDeclarationSyntax? targetClass = root.DescendantNodes()
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

            // Get members to include in GetHashCode
            List<(string Name, string Type)> members = GetMembersForComparison(targetClass, options);

            if (members.Count == 0)
            {
                return new CodeGenerationResult
                {
                    Success = false,
                    Error = "No fields or properties found to generate GetHashCode method"
                };
            }

            // Generate GetHashCode method code
            string hashCodeMethod = GenerateGetHashCodeMethodCode(members, options);

            // Check if GetHashCode already exists
            bool hasExistingHashCode = targetClass.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Any(m => m.Identifier.ValueText == "GetHashCode" &&
                         m.ParameterList.Parameters.Count == 0);

            if (hasExistingHashCode)
            {
                return new CodeGenerationResult
                {
                    Success = false,
                    Error = "GetHashCode method already exists in the class"
                };
            }

            // Insert method into class
            string modifiedContent = InsertMethodIntoClass(sourceCode, targetClass, hashCodeMethod);

            if (!previewOnly)
            {
                await File.WriteAllTextAsync(resolvedFilePath, modifiedContent, cancellationToken);
            }

            return new CodeGenerationResult
            {
                Success = true,
                Message = previewOnly
                    ? $"Preview: Would generate GetHashCode method for '{className}' with {members.Count} members"
                    : $"Successfully generated GetHashCode method for '{className}' with {members.Count} members",
                GeneratedCode = hashCodeMethod,
                ModifiedFileContent = modifiedContent,
                FilePath = resolvedFilePath,
                Metadata = new Dictionary<string, object>
                {
                    ["className"] = className,
                    ["memberCount"] = members.Count,
                    ["membersIncluded"] = members.Select(m => m.Name).ToArray()
                }
            };
        }
        catch (Exception ex)
        {
            return new CodeGenerationResult
            {
                Success = false,
                Error = $"GetHashCode method generation failed: {ex.Message}"
            };
        }
    }

    #region Helper Methods

    private static List<(string Name, string Type)> GetMembersForComparison(
        ClassDeclarationSyntax targetClass,
        GenerateMethodOptions options)
    {
        var members = new List<(string Name, string Type)>();

        if (options.IncludeAllFields)
        {
            IEnumerable<(string ValueText, string)> fields = targetClass.DescendantNodes()
                .OfType<FieldDeclarationSyntax>()
                .Where(f => !f.Modifiers.Any(SyntaxKind.StaticKeyword))
                .SelectMany(f => f.Declaration.Variables)
                .Select(v => (v.Identifier.ValueText, GetFieldType(v)));

            members.AddRange(fields);
        }

        if (options.IncludeAllProperties)
        {
            IEnumerable<(string ValueText, string)> properties = targetClass.DescendantNodes()
                .OfType<PropertyDeclarationSyntax>()
                .Where(p => !p.Modifiers.Any(SyntaxKind.StaticKeyword) &&
                           p.AccessorList?.Accessors.Any(a => a.Keyword.IsKind(SyntaxKind.GetKeyword)) == true)
                .Select(p => (p.Identifier.ValueText, p.Type.ToString()));

            members.AddRange(properties);
        }

        // Filter by included members if specified
        if (options.IncludedMembers.Count != 0)
        {
            members = members.Where(m => options.IncludedMembers.Contains(m.Name)).ToList();
        }

        return members;
    }

    private static string GenerateEqualsMethodCode(
        string className,
        List<(string Name, string Type)> members,
        GenerateMethodOptions options)
    {
        IEnumerable<string> comparisons = members.Select(m =>
            IsReferenceType(m.Type)
                ? $"Equals({m.Name}, other.{m.Name})"
                : $"{m.Name} == other.{m.Name}"
        );

        return $$"""

                     {{options.AccessModifier}} override bool Equals(object obj)
                     {
                         if (ReferenceEquals(null, obj)) return false;
                         if (ReferenceEquals(this, obj)) return true;
                         if (obj.GetType() != GetType()) return false;
                         return Equals(({{className}})obj);
                     }

                     {{options.AccessModifier}} bool Equals({{className}} other)
                     {
                         return {{string.Join(" && ", comparisons)}};
                     }
                 """;
    }

    private static string GenerateGetHashCodeMethodCode(
        List<(string Name, string Type)> members,
        GenerateMethodOptions options)
    {
        if (members.Count == 1)
        {
            (string Name, string Type) member = members.First();
            return $$"""

                         {{options.AccessModifier}} override int GetHashCode()
                         {
                             return {{member.Name}}?.GetHashCode() ?? 0;
                         }
                     """;
        }

        IEnumerable<string> hashItems = members.Select(m =>
            IsReferenceType(m.Type) ? $"{m.Name}?.GetHashCode() ?? 0" : $"{m.Name}.GetHashCode()"
        );

        return $$"""

                     {{options.AccessModifier}} override int GetHashCode()
                     {
                         return HashCode.Combine({{string.Join(", ", hashItems.Take(8))}});
                     }
                 """;
    }

    private static string InsertMethodIntoClass(
        string sourceCode,
        ClassDeclarationSyntax targetClass,
        string methodCode)
    {
        // Find a good insertion point (at the end of the class, before closing brace)
        string[] lines = sourceCode.Split('\n');
        int insertionLine = FindInsertionPointForMethod(lines, targetClass);

        List<string> modifiedLines = lines.ToList();
        modifiedLines.Insert(insertionLine, methodCode);

        return string.Join("\n", modifiedLines);
    }

    private static int FindInsertionPointForMethod(string[] lines, ClassDeclarationSyntax targetClass)
    {
        // Simple heuristic: insert before closing brace of class
        int classEndLine = targetClass.GetLocation().GetLineSpan().EndLinePosition.Line;

        // Look for closing brace from the end
        for (int i = classEndLine; i >= 0; i--)
        {
            if (lines[i].Trim() == "}")
            {
                return i;
            }
        }

        return classEndLine;
    }

    private static string GetFieldType(VariableDeclaratorSyntax variable)
    {
        var field = variable.FirstAncestorOrSelf<FieldDeclarationSyntax>();
        return field?.Declaration?.Type?.ToString() ?? "object";
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
        string fullPath = Path.IsPathRooted(path) ? path : Path.Combine(config.DefaultWorkspace, path);
        fullPath = Path.GetFullPath(fullPath);

        // Security check: ensure path is within workspace if restricted
        if (config.Security.RestrictToWorkspace)
        {
            string workspaceFullPath = Path.GetFullPath(config.DefaultWorkspace);
            if (!fullPath.StartsWith(workspaceFullPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException($"Access denied: Path outside workspace: {path}");
            }
        }

        // Check blocked paths
        foreach (string blockedPath in config.Security.BlockedPaths)
        {
            string blockedFullPath = Path.GetFullPath(blockedPath);
            if (fullPath.StartsWith(blockedFullPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException($"Access denied: Blocked path: {path}");
            }
        }

        return fullPath;
    }

    #endregion
}
