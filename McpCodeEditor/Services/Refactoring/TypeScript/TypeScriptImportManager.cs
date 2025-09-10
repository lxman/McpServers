using McpCodeEditor.Interfaces;
using McpCodeEditor.Models;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using McpCodeEditor.Models.Refactoring;

namespace McpCodeEditor.Services.Refactoring.TypeScript;

/// <summary>
/// Service for managing TypeScript import statements
/// Handles import organization, sorting, grouping, and addition of new import statements
/// Enhanced with Angular-specific logic for improved project organization
/// </summary>
public class TypeScriptImportManager(
    ILogger<TypeScriptImportManager> logger,
    IPathValidationService pathValidationService)
    : ITypeScriptImportManager
{
    /// <summary>
    /// Organize and sort TypeScript import statements with Angular-specific grouping
    /// </summary>
    public async Task<RefactoringResult> OrganizeImportsAsync(
        string filePath,
        bool sortAlphabetically = true,
        bool groupByType = true,
        bool removeUnused = false,
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Starting TypeScript import organization: {FilePath}", filePath);

            // Validate file path
            string resolvedPath;
            try
            {
                resolvedPath = pathValidationService.ValidateAndResolvePath(filePath);
            }
            catch (Exception ex)
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }

            // Validate TypeScript file
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            if (!IsTypeScriptFile(extension))
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"File is not a TypeScript/JavaScript file: {extension}"
                };
            }

            // Read file content
            string[] lines = await File.ReadAllLinesAsync(resolvedPath, cancellationToken);

            // Parse existing imports
            List<TypeScriptImport> imports = ParseImports(lines);
            if (imports.Count == 0)
            {
                return new RefactoringResult
                {
                    Success = true,
                    Message = "No import statements found to organize"
                };
            }

            // Detect if this is an Angular project
            bool isAngularProject = DetectAngularProject(imports, resolvedPath);

            // Remove unused imports if requested
            if (removeUnused)
            {
                imports = RemoveUnusedImports(imports, lines);
            }

            // Organize imports with Angular-specific logic
            List<TypeScriptImport> organizedImports = OrganizeImportStatements(imports, sortAlphabetically, groupByType, isAngularProject);

            // Generate new import section
            List<string> newImportLines = GenerateImportLines(organizedImports, isAngularProject);

            // Find import section boundaries
            ImportBounds importBounds = FindImportBounds(lines);
            if (!importBounds.HasImports)
            {
                return new RefactoringResult
                {
                    Success = true,
                    Message = "No import statements found to organize"
                };
            }

            // Create modified content
            var modifiedLines = new List<string>();

            // Add content before imports
            modifiedLines.AddRange(lines.Take(importBounds.StartLine));

            // Add organized imports
            modifiedLines.AddRange(newImportLines);

            // Add content after imports
            modifiedLines.AddRange(lines.Skip(importBounds.EndLine + 1));

            // Create FileChange for change tracking
            var changes = new List<FileChange>
            {
                new FileChange
                {
                    FilePath = filePath,
                    OriginalContent = string.Join(Environment.NewLine, lines),
                    ModifiedContent = string.Join(Environment.NewLine, modifiedLines),
                    ChangeType = "OrganizeTypeScriptImports"
                }
            };

            if (previewOnly)
            {
                string projectType = isAngularProject ? "Angular" : "TypeScript";
                return new RefactoringResult
                {
                    Success = true,
                    Message = $"Preview: Would organize {imports.Count} {projectType} imports",
                    Changes = changes
                };
            }

            // Write modified content
            await File.WriteAllLinesAsync(resolvedPath, modifiedLines, cancellationToken);

            string projectTypeMsg = isAngularProject ? "Angular" : "TypeScript";
            return new RefactoringResult
            {
                Success = true,
                Message = $"Successfully organized {imports.Count} {projectTypeMsg} imports",
                FilesAffected = 1,
                Changes = changes
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TypeScript import organization failed for {FilePath}", filePath);
            return new RefactoringResult
            {
                Success = false,
                Error = $"TypeScript import organization failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Add an import statement to a TypeScript file
    /// </summary>
    public async Task<RefactoringResult> AddImportAsync(
        string filePath,
        string importStatement,
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Adding TypeScript import: {ImportStatement} to {FilePath}", 
                importStatement, filePath);

            // Validate file path
            string resolvedPath;
            try
            {
                resolvedPath = pathValidationService.ValidateAndResolvePath(filePath);
            }
            catch (Exception ex)
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }

            // Validate TypeScript file
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            if (!IsTypeScriptFile(extension))
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"File is not a TypeScript/JavaScript file: {extension}"
                };
            }

            // Validate import statement
            if (!IsValidImportStatement(importStatement))
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = "Invalid import statement format"
                };
            }

            // Read file content
            string[] lines = await File.ReadAllLinesAsync(resolvedPath, cancellationToken);

            // Parse existing imports
            List<TypeScriptImport> existingImports = ParseImports(lines);

            // Check if import already exists
            if (ImportAlreadyExists(existingImports, importStatement))
            {
                return new RefactoringResult
                {
                    Success = true,
                    Message = "Import statement already exists"
                };
            }

            // Detect if this is an Angular project for better placement
            bool isAngularProject = DetectAngularProject(existingImports, resolvedPath);

            // Find insertion point with Angular-aware logic
            int insertionPoint = FindImportInsertionPoint(lines, importStatement, isAngularProject);

            // Create modified content
            var modifiedLines = new List<string>(lines);
            modifiedLines.Insert(insertionPoint, importStatement);

            var changes = new List<FileChange>
            {
                new FileChange
                {
                    FilePath = filePath,
                    OriginalContent = string.Join(Environment.NewLine, lines),
                    ModifiedContent = string.Join(Environment.NewLine, modifiedLines),
                    ChangeType = "AddTypeScriptImport"
                }
            };

            if (previewOnly)
            {
                return new RefactoringResult
                {
                    Success = true,
                    Message = "Preview: Would add TypeScript import statement",
                    Changes = changes
                };
            }

            // Write modified content
            await File.WriteAllLinesAsync(resolvedPath, modifiedLines, cancellationToken);

            return new RefactoringResult
            {
                Success = true,
                Message = "Successfully added TypeScript import statement",
                FilesAffected = 1,
                Changes = changes
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Add TypeScript import failed for {FilePath}", filePath);
            return new RefactoringResult
            {
                Success = false,
                Error = $"Add TypeScript import failed: {ex.Message}"
            };
        }
    }

    #region Private Helper Methods

    private static bool IsTypeScriptFile(string extension)
    {
        return extension switch
        {
            ".ts" or ".tsx" or ".js" or ".jsx" => true,
            _ => false
        };
    }

    /// <summary>
    /// Detect if this is an Angular project based on imports and file structure
    /// </summary>
    private static bool DetectAngularProject(List<TypeScriptImport> imports, string filePath)
    {
        // Check for Angular imports
        bool hasAngularImports = imports.Any(i => 
            i.ModulePath.StartsWith("@angular/") ||
            i.ModulePath.StartsWith("@angular-") ||
            i.ModulePath.Contains("rxjs"));

        // Check for Angular file patterns
        bool hasAngularFilePattern = filePath.Contains(".component.") ||
                                   filePath.Contains(".service.") ||
                                   filePath.Contains(".module.") ||
                                   filePath.Contains(".directive.") ||
                                   filePath.Contains(".pipe.");

        // Check for Angular project structure
        bool inAngularProject = filePath.Contains("src/app/") ||
                              filePath.Contains("\\src\\app\\") ||
                              Directory.Exists(Path.Combine(Path.GetDirectoryName(filePath) ?? "", "../../node_modules/@angular"));

        return hasAngularImports || hasAngularFilePattern || inAngularProject;
    }

    private static List<TypeScriptImport> ParseImports(string[] lines)
    {
        var imports = new List<TypeScriptImport>();
        var importPattern = @"^import\s+(.+)\s+from\s+['""]([^'""]+)['""];?\s*$";

        for (var i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            Match match = Regex.Match(line, importPattern);
            
            if (match.Success)
            {
                string modulePath = match.Groups[2].Value.Trim();
                imports.Add(new TypeScriptImport
                {
                    LineNumber = i + 1,
                    FullStatement = line,
                    ImportClause = match.Groups[1].Value.Trim(),
                    ModulePath = modulePath,
                    ImportType = DetermineImportType(modulePath)
                });
            }
        }

        return imports;
    }

    /// <summary>
    /// Enhanced import type detection with Angular-specific categories
    /// </summary>
    private static TypeScriptImportType DetermineImportType(string modulePath)
    {
        // Angular Core imports
        if (modulePath.StartsWith("@angular/"))
            return TypeScriptImportType.AngularCore;
        
        // Angular Material/CDK
        if (modulePath.StartsWith("@angular/material") || modulePath.StartsWith("@angular/cdk"))
            return TypeScriptImportType.AngularMaterial;
        
        // RxJS (commonly used with Angular)
        if (modulePath.StartsWith("rxjs"))
            return TypeScriptImportType.RxJs;
        
        // Relative imports (application code)
        if (modulePath.StartsWith("./") || modulePath.StartsWith("../"))
            return TypeScriptImportType.Relative;
        
        // Scoped libraries (e.g., @company/library)
        if (modulePath.StartsWith("@") && !modulePath.StartsWith("@angular"))
            return TypeScriptImportType.ScopedLibrary;
        
        // Path aliases
        if (modulePath.StartsWith("~"))
            return TypeScriptImportType.Alias;
        
        // Regular npm libraries
        return TypeScriptImportType.Library;
    }

    private static List<TypeScriptImport> RemoveUnusedImports(List<TypeScriptImport> imports, string[] lines)
    {
        var usedImports = new List<TypeScriptImport>();

        foreach (TypeScriptImport import in imports)
        {
            if (IsImportUsed(import, lines))
            {
                usedImports.Add(import);
            }
        }

        return usedImports;
    }

    private static bool IsImportUsed(TypeScriptImport import, string[] lines)
    {
        // Extract imported symbols
        List<string> symbols = ExtractImportedSymbols(import.ImportClause);
        
        // Check if any symbol is used in the code
        foreach (string symbol in symbols)
        {
            for (var i = 0; i < lines.Length; i++)
            {
                if (i == import.LineNumber - 1) continue; // Skip the import line itself
                
                if (lines[i].Contains(symbol))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static List<string> ExtractImportedSymbols(string importClause)
    {
        var symbols = new List<string>();

        // Handle different import patterns
        // import defaultExport from 'module'
        // import { named1, named2 } from 'module'
        // import defaultExport, { named1, named2 } from 'module'
        // import * as namespace from 'module'

        if (importClause.Contains("* as "))
        {
            Match match = Regex.Match(importClause, @"\*\s+as\s+(\w+)");
            if (match.Success)
            {
                symbols.Add(match.Groups[1].Value);
            }
        }
        else if (importClause.Contains("{"))
        {
            Match braceMatch = Regex.Match(importClause, @"\{([^}]+)\}");
            if (braceMatch.Success)
            {
                IEnumerable<string> namedImports = braceMatch.Groups[1].Value
                    .Split(',')
                    .Select(s => s.Trim().Split(' ')[0]) // Handle 'as' aliases
                    .Where(s => !string.IsNullOrEmpty(s));
                
                symbols.AddRange(namedImports);
            }

            // Handle default import before braces
            Match defaultMatch = Regex.Match(importClause, @"^(\w+),");
            if (defaultMatch.Success)
            {
                symbols.Add(defaultMatch.Groups[1].Value);
            }
        }
        else
        {
            // Default import
            Match defaultMatch = Regex.Match(importClause, @"^(\w+)");
            if (defaultMatch.Success)
            {
                symbols.Add(defaultMatch.Groups[1].Value);
            }
        }

        return symbols;
    }

    /// <summary>
    /// Enhanced import organization with Angular-specific grouping
    /// </summary>
    private static List<TypeScriptImport> OrganizeImportStatements(
        List<TypeScriptImport> imports,
        bool sortAlphabetically,
        bool groupByType,
        bool isAngularProject)
    {
        if (!groupByType && !sortAlphabetically)
            return imports;

        var organized = new List<TypeScriptImport>();

        if (groupByType)
        {
            List<TypeScriptImport>[] groups;
            
            if (isAngularProject)
            {
                // Angular-specific grouping order
                groups = [
                    imports.Where(i => i.ImportType == TypeScriptImportType.AngularCore).ToList(),
                    imports.Where(i => i.ImportType == TypeScriptImportType.AngularMaterial).ToList(),
                    imports.Where(i => i.ImportType == TypeScriptImportType.RxJs).ToList(),
                    imports.Where(i => i.ImportType == TypeScriptImportType.Library).ToList(),
                    imports.Where(i => i.ImportType == TypeScriptImportType.ScopedLibrary).ToList(),
                    imports.Where(i => i.ImportType == TypeScriptImportType.Alias).ToList(),
                    imports.Where(i => i.ImportType == TypeScriptImportType.Relative).ToList()
                ];
            }
            else
            {
                // Standard TypeScript grouping
                groups = [
                    imports.Where(i => i.ImportType == TypeScriptImportType.Library).ToList(),
                    imports.Where(i => i.ImportType == TypeScriptImportType.ScopedLibrary).ToList(),
                    imports.Where(i => i.ImportType == TypeScriptImportType.Alias).ToList(),
                    imports.Where(i => i.ImportType == TypeScriptImportType.Relative).ToList()
                ];
            }

            for (var i = 0; i < groups.Length; i++)
            {
                List<TypeScriptImport> group = groups[i];
                if (group.Count == 0) continue;

                if (sortAlphabetically)
                {
                    group.Sort((a, b) => string.Compare(a.ModulePath, b.ModulePath, StringComparison.OrdinalIgnoreCase));
                }

                organized.AddRange(group);
                
                // Add blank line between groups (except after the last group)
                if (i < groups.Length - 1 && group.Count > 0 && groups.Skip(i + 1).Any(g => g.Count > 0))
                {
                    // Mark for blank line insertion
                    if (group.Count > 0)
                        group.Last().AddBlankLineAfter = true;
                }
            }
        }
        else if (sortAlphabetically)
        {
            organized = imports.OrderBy(i => i.ModulePath, StringComparer.OrdinalIgnoreCase).ToList();
        }

        return organized;
    }

    /// <summary>
    /// Generate import lines with Angular-specific formatting
    /// </summary>
    private static List<string> GenerateImportLines(List<TypeScriptImport> imports, bool isAngularProject)
    {
        var lines = new List<string>();
        
        foreach (TypeScriptImport import in imports)
        {
            lines.Add(import.FullStatement);
            
            // Add blank line after certain groups in Angular projects
            if (isAngularProject && import.AddBlankLineAfter)
            {
                lines.Add("");
            }
        }
        
        return lines;
    }

    private static ImportBounds FindImportBounds(string[] lines)
    {
        int startLine = -1;
        int endLine = -1;

        for (var i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            
            if (line.StartsWith("import ") && startLine == -1)
            {
                startLine = i;
            }
            
            if (line.StartsWith("import "))
            {
                endLine = i;
            }
        }

        return new ImportBounds
        {
            HasImports = startLine != -1,
            StartLine = startLine,
            EndLine = endLine
        };
    }

    private static bool IsValidImportStatement(string importStatement)
    {
        var pattern = @"^import\s+.+\s+from\s+['""].+['""];?\s*$";
        return Regex.IsMatch(importStatement.Trim(), pattern);
    }

    private static bool ImportAlreadyExists(List<TypeScriptImport> existingImports, string newImportStatement)
    {
        string normalizedNew = newImportStatement.Trim().TrimEnd(';');
        
        return existingImports.Any(import => 
        {
            string normalizedExisting = import.FullStatement.Trim().TrimEnd(';');
            return string.Equals(normalizedExisting, normalizedNew, StringComparison.OrdinalIgnoreCase);
        });
    }

    /// <summary>
    /// Enhanced import insertion with Angular-aware positioning
    /// </summary>
    private static int FindImportInsertionPoint(string[] lines, string importStatement, bool isAngularProject)
    {
        var newImport = new TypeScriptImport
        {
            FullStatement = importStatement,
            ImportType = DetermineImportType(ExtractModulePath(importStatement))
        };

        List<TypeScriptImport> existingImports = ParseImports(lines);
        
        if (existingImports.Count == 0)
        {
            // Insert at the beginning of the file
            return 0;
        }

        // Find appropriate position based on import type with Angular logic
        for (var i = 0; i < existingImports.Count; i++)
        {
            if (ShouldInsertBefore(newImport, existingImports[i], isAngularProject))
            {
                return existingImports[i].LineNumber - 1;
            }
        }

        // Insert after last import
        return existingImports.Last().LineNumber;
    }

    private static string ExtractModulePath(string importStatement)
    {
        Match match = Regex.Match(importStatement, @"from\s+['""]([^'""]+)['""]");
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    /// <summary>
    /// Enhanced insertion logic with Angular-specific priorities
    /// </summary>
    private static bool ShouldInsertBefore(TypeScriptImport newImport, TypeScriptImport existingImport, bool isAngularProject)
    {
        // Insert based on import type priority
        int newPriority = GetImportTypePriority(newImport.ImportType, isAngularProject);
        int existingPriority = GetImportTypePriority(existingImport.ImportType, isAngularProject);

        if (newPriority != existingPriority)
            return newPriority < existingPriority;

        // Same type, sort alphabetically
        return string.Compare(newImport.ModulePath, existingImport.ModulePath, StringComparison.OrdinalIgnoreCase) < 0;
    }

    /// <summary>
    /// Get import type priority with Angular-specific ordering
    /// </summary>
    private static int GetImportTypePriority(TypeScriptImportType importType, bool isAngularProject)
    {
        if (isAngularProject)
        {
            return importType switch
            {
                TypeScriptImportType.AngularCore => 1,
                TypeScriptImportType.AngularMaterial => 2,
                TypeScriptImportType.RxJs => 3,
                TypeScriptImportType.Library => 4,
                TypeScriptImportType.ScopedLibrary => 5,
                TypeScriptImportType.Alias => 6,
                TypeScriptImportType.Relative => 7,
                _ => 8
            };
        }
        
        return importType switch
        {
            TypeScriptImportType.Library => 1,
            TypeScriptImportType.ScopedLibrary => 2,
            TypeScriptImportType.Alias => 3,
            TypeScriptImportType.Relative => 4,
            _ => 5
        };
    }

    #endregion
}

// Supporting interfaces and models
public interface ITypeScriptImportManager
{
    Task<RefactoringResult> OrganizeImportsAsync(
        string filePath,
        bool sortAlphabetically = true,
        bool groupByType = true,
        bool removeUnused = false,
        bool previewOnly = false,
        CancellationToken cancellationToken = default);

    Task<RefactoringResult> AddImportAsync(
        string filePath,
        string importStatement,
        bool previewOnly = false,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Enhanced TypeScript import model with Angular support
/// </summary>
public class TypeScriptImport
{
    public int LineNumber { get; set; }
    public string FullStatement { get; set; } = string.Empty;
    public string ImportClause { get; set; } = string.Empty;
    public string ModulePath { get; set; } = string.Empty;
    public TypeScriptImportType ImportType { get; set; }
    public bool AddBlankLineAfter { get; set; } // For Angular grouping
}

/// <summary>
/// Enhanced TypeScript import types with Angular-specific categories
/// </summary>
public enum TypeScriptImportType
{
    AngularCore,        // @angular/core, @angular/common, etc.
    AngularMaterial,    // @angular/material, @angular/cdk
    RxJs,              // rxjs imports
    Library,           // npm libraries
    ScopedLibrary,     // @scoped/library
    Alias,             // ~alias imports
    Relative,          // ./relative imports
    Other
}

public class ImportBounds
{
    public bool HasImports { get; set; }
    public int StartLine { get; set; }
    public int EndLine { get; set; }
}
