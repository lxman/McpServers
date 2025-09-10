namespace McpCodeEditor.Models.Refactoring.CSharp;

/// <summary>
/// Represents options for C# import (using statement) operations.
/// Used to configure how using statements should be organized and managed.
/// </summary>
public class CSharpImportOperation
{
    /// <summary>
    /// Whether to remove unused using statements.
    /// Note: This requires semantic analysis and is currently a placeholder.
    /// </summary>
    public bool RemoveUnused { get; set; } = true;

    /// <summary>
    /// Whether to sort using statements alphabetically.
    /// </summary>
    public bool SortAlphabetically { get; set; } = true;

    /// <summary>
    /// Whether to group using statements by type (System first, then others).
    /// </summary>
    public bool GroupByType { get; set; } = true;

    /// <summary>
    /// Whether to separate System using statements from user namespaces.
    /// </summary>
    public bool SeparateSystemNamespaces { get; set; } = true;

    /// <summary>
    /// Whether to remove duplicate using statements.
    /// </summary>
    public bool RemoveDuplicates { get; set; } = true;

    /// <summary>
    /// Custom ordering rules for specific namespaces.
    /// Key is namespace, value is priority (lower numbers appear first).
    /// </summary>
    public Dictionary<string, int> CustomOrdering { get; set; } = new();

    /// <summary>
    /// Namespaces to always keep even if they appear unused.
    /// Useful for global using statements or required namespaces.
    /// </summary>
    public HashSet<string> AlwaysKeep { get; set; } = [];

    /// <summary>
    /// Whether to add blank lines between different groups of using statements.
    /// </summary>
    public bool AddBlankLinesBetweenGroups { get; set; } = true;
}

/// <summary>
/// Represents the analysis results of using statements in a C# file.
/// </summary>
public class CSharpImportAnalysis
{
    /// <summary>
    /// Total number of using statements found.
    /// </summary>
    public int TotalUsings { get; set; }

    /// <summary>
    /// Number of System namespace using statements.
    /// </summary>
    public int SystemUsings { get; set; }

    /// <summary>
    /// Number of user namespace using statements.
    /// </summary>
    public int UserUsings { get; set; }

    /// <summary>
    /// Number of duplicate using statements found.
    /// </summary>
    public int DuplicateUsings { get; set; }

    /// <summary>
    /// List of all using statements found in the file.
    /// </summary>
    public List<CSharpUsingStatement> Usings { get; set; } = [];

    /// <summary>
    /// Detected issues with the current using statements.
    /// </summary>
    public List<string> Issues { get; set; } = [];

    /// <summary>
    /// Suggestions for improving the using statements organization.
    /// </summary>
    public List<string> Suggestions { get; set; } = [];
}

/// <summary>
/// Represents a single C# using statement with analysis information.
/// </summary>
public class CSharpUsingStatement
{
    /// <summary>
    /// The namespace being imported (e.g., "System.Collections.Generic").
    /// </summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// Line number where this using statement appears (1-based).
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// Whether this is a System namespace.
    /// </summary>
    public bool IsSystemNamespace { get; set; }

    /// <summary>
    /// Whether this using statement appears to be unused.
    /// Note: This requires semantic analysis and is currently a placeholder.
    /// </summary>
    public bool IsUnused { get; set; }

    /// <summary>
    /// Whether this is a duplicate of another using statement.
    /// </summary>
    public bool IsDuplicate { get; set; }

    /// <summary>
    /// The full original text of the using statement.
    /// </summary>
    public string OriginalText { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is a global using statement (C# 10+).
    /// </summary>
    public bool IsGlobal { get; set; }

    /// <summary>
    /// Whether this is an alias using statement (e.g., "using MyAlias = SomeNamespace").
    /// </summary>
    public bool IsAlias { get; set; }

    /// <summary>
    /// The alias name if this is an alias using statement.
    /// </summary>
    public string? AliasName { get; set; }
}
