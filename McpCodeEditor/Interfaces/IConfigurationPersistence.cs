using McpCodeEditor.Services;

namespace McpCodeEditor.Interfaces;

/// <summary>
/// Interface for persisting and loading configuration data
/// </summary>
public interface IConfigurationPersistence
{
    /// <summary>
    /// Save configuration data to persistent storage
    /// </summary>
    Task SaveConfigurationAsync(CodeEditorConfiguration configuration);
    
    /// <summary>
    /// Load configuration data from persistent storage
    /// </summary>
    Task<CodeEditorConfiguration?> LoadConfigurationAsync();
    
    /// <summary>
    /// Check if configuration exists in persistent storage
    /// </summary>
    Task<bool> ConfigurationExistsAsync();
    
    /// <summary>
    /// Get the path where configuration is stored
    /// </summary>
    string GetConfigurationPath();
}
