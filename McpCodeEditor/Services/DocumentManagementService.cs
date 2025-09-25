using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using McpCodeEditor.Interfaces;

namespace McpCodeEditor.Services;

/// <summary>
/// Service responsible for managing documents within Roslyn workspaces
/// Extracted from SymbolNavigationService - Phase 4 Task 2b
/// </summary>
public class DocumentManagementService : IDocumentManagementService
{
    private readonly IWorkspaceManagementService _workspaceManagement;
    private readonly ILogger<DocumentManagementService>? _logger;

    public DocumentManagementService(
        IWorkspaceManagementService workspaceManagement,
        ILogger<DocumentManagementService>? logger = null)
    {
        _workspaceManagement = workspaceManagement;
        _logger = logger;
    }

    /// <summary>
    /// Get a document from the current workspace by file path
    /// </summary>
    public Document? GetDocument(string filePath)
    {
        try
        {
            var currentSolution = _workspaceManagement.CurrentSolution;
            if (currentSolution == null) 
                return null;

            return currentSolution.Projects
                .SelectMany(p => p.Documents)
                .FirstOrDefault(d => d.FilePath != null &&
                    Path.GetFullPath(d.FilePath).Equals(Path.GetFullPath(filePath), StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, $"Failed to get document: {filePath}");
            return null;
        }
    }

    /// <summary>
    /// Add a document to the fallback workspace if using fallback mode
    /// </summary>
    public async Task<Document?> AddDocumentToFallbackWorkspaceAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            // Only works with fallback workspace
            if (!_workspaceManagement.IsUsingFallbackWorkspace)
            {
                _logger?.LogDebug($"Not using fallback workspace, cannot add document: {filePath}");
                return null;
            }

            var currentSolution = _workspaceManagement.CurrentSolution;
            if (currentSolution == null)
            {
                _logger?.LogWarning("No current solution available for adding document");
                return null;
            }

            // Get the first project (fallback workspace typically has one project)
            var project = currentSolution.Projects.FirstOrDefault();
            if (project == null)
            {
                _logger?.LogWarning("No project available in fallback workspace for adding document");
                return null;
            }

            // Validate file exists and can be read
            if (!IsValidDocumentPath(filePath))
            {
                _logger?.LogWarning($"Invalid document path: {filePath}");
                return null;
            }

            // Read file content
            var fileContent = await File.ReadAllTextAsync(filePath, cancellationToken);
            
            // Create document info
            var documentInfo = DocumentInfo.Create(
                DocumentId.CreateNewId(project.Id),
                Path.GetFileName(filePath),
                loader: TextLoader.From(TextAndVersion.Create(
                    SourceText.From(fileContent),
                    VersionStamp.Create())),
                filePath: filePath);

            // Note: In the current architecture, we can't directly apply changes through the workspace
            // This is a limitation that would require coordination with WorkspaceManagementService
            // For now, we'll log this limitation and return null
            _logger?.LogWarning($"Cannot add document to workspace in current architecture: {filePath}");
            _logger?.LogInformation("Document addition requires workspace coordination - returning null");
            
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, $"Failed to add document to fallback workspace: {filePath}");
            return null;
        }
    }

    /// <summary>
    /// Try to resolve a document, adding it to fallback workspace if necessary
    /// </summary>
    public async Task<Document?> ResolveDocumentAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            // First, try to get the document from the current workspace
            var document = GetDocument(filePath);
            if (document != null)
            {
                _logger?.LogDebug($"Document found in workspace: {filePath}");
                return document;
            }

            // If not found and using fallback workspace, try to add it
            if (_workspaceManagement.IsUsingFallbackWorkspace && IsValidDocumentPath(filePath))
            {
                _logger?.LogInformation($"Document not in workspace, attempting to add to fallback: {filePath}");
                document = await AddDocumentToFallbackWorkspaceAsync(filePath, cancellationToken);
                
                if (document != null)
                {
                    _logger?.LogInformation($"Successfully added document to fallback workspace: {filePath}");
                    return document;
                }
            }

            _logger?.LogInformation($"Could not resolve document: {filePath}");
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, $"Failed to resolve document: {filePath}");
            return null;
        }
    }

    /// <summary>
    /// Check if a file path exists and is valid for document operations
    /// </summary>
    public bool IsValidDocumentPath(string filePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            // Check if file exists
            if (!File.Exists(filePath))
                return false;

            // Check if it's a supported file type (primarily C# for now)
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var supportedExtensions = new[] { ".cs", ".csx", ".vb", ".fs", ".fsx" };
            
            if (!supportedExtensions.Contains(extension))
            {
                _logger?.LogDebug($"Unsupported file extension: {extension} for file: {filePath}");
                return false;
            }

            // Check if file is readable
            try
            {
                using var stream = File.OpenRead(filePath);
                return true;
            }
            catch
            {
                _logger?.LogDebug($"File is not readable: {filePath}");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, $"Error validating document path: {filePath}");
            return false;
        }
    }
}
