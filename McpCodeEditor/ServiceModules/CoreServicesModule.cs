using McpCodeEditor.Interfaces;
using McpCodeEditor.Services;
using McpCodeEditor.Services.Security;
using Microsoft.Extensions.DependencyInjection;

namespace McpCodeEditor.ServiceModules;

/// <summary>
/// Registration module for core application services
/// Extracted from Program.cs to improve maintainability and organization
/// </summary>
public static class CoreServicesModule
{
    /// <summary>
    /// Register all core services with the DI container
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        // Developer Environment Initialization - MUST BE FIRST
        services.AddSingleton<DeveloperEnvironmentService>();
        
        // Workspace management service (depends on DeveloperEnvironmentService) - Phase 4 Task 2a
        services.AddSingleton<IWorkspaceManagementService, WorkspaceManagementService>();
        
        // Document management service (depends on IWorkspaceManagementService) - Phase 4 Task 2b
        services.AddSingleton<IDocumentManagementService, DocumentManagementService>();
        
        // Semantic analysis service (core Roslyn semantic operations) - Phase 4 Task 2c
        services.AddSingleton<ISemanticAnalysisService, SemanticAnalysisService>();
        
        // Advanced File Reader services - Advanced file reading with Roslyn power
        services.AddAdvancedFileReaderServices();
        
        // Backup and change tracking services
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<IChangeTrackingService, ChangeTrackingService>();
        
        // Content snapshot service (Phase 4 Task 3a) - For change tracking snapshots
        services.AddSingleton<IContentSnapshotService, ContentSnapshotService>();
        
        // Change record persistence service (Phase 4 Task 3b) - For low-level change record operations
        services.AddSingleton<IChangeRecordPersistenceService, ChangeRecordPersistenceService>();
        
        // Undo/Redo operations service (Phase 4 Task 3b) - For undo/redo logic
        services.AddSingleton<IUndoRedoOperationsService, UndoRedoOperationsService>();
        
        // Change statistics service (Phase 4 Task 3c) - For change analytics and statistics
        services.AddSingleton<IChangeStatisticsService, ChangeStatisticsService>();
        
        // Configuration and persistence services
        services.AddSingleton<IConfigurationPersistence, JsonConfigurationPersistence>();
        
        // File Storage Architecture - %APPDATA% path services 
        services.AddSingleton<IAppDataPathService, AppDataPathService>();  
        services.AddSingleton<IWorkspaceMetadataService, WorkspaceMetadataService>();
        
        // Core detection and scale services
        services.AddSingleton<ProjectDetectionService>();
        services.AddSingleton<ProjectScaleService>();
        
        // Configuration service (depends on ProjectDetectionService + IConfigurationPersistence)
        services.AddSingleton<CodeEditorConfigurationService>();
        
        // Path validation and security
        services.AddSingleton<IPathValidationService, PathValidationService>();
        
        // File and code services
        
        // Type Research Attestation Service - Required for code file creation safety
        services.AddSingleton<TypeResearchAttestationService>();
        services.AddSingleton<FileOperationsService>();
        services.AddSingleton<CodeAnalysisService>();
        services.AddSingleton<ConversionService>();
        services.AddSingleton<DiffService>();
        services.AddSingleton<GitService>();
        
        // Additional analysis services
        services.AddSingleton<WorkspaceAnalysisService>();
        services.AddSingleton<WorkspaceStatsService>(); 
        services.AddSingleton<WorkspaceInfoService>();
        
        return services;
    }
}