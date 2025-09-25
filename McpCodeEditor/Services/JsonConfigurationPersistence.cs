using System.Text.Json;
using McpCodeEditor.Interfaces;

namespace McpCodeEditor.Services;

/// <summary>
/// JSON-based implementation of configuration persistence
/// Stores user preferences in %APPDATA%/McpCodeEditor/config.json
/// </summary>
public class JsonConfigurationPersistence : IConfigurationPersistence
{
    private readonly string _configPath;
    private readonly JsonSerializerOptions _jsonOptions;

    public JsonConfigurationPersistence()
    {
        // Store in %APPDATA%/McpCodeEditor/config.json
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var mcpDirectory = Path.Combine(appDataPath, "McpCodeEditor");
        
        // Ensure directory exists
        Directory.CreateDirectory(mcpDirectory);
        
        _configPath = Path.Combine(mcpDirectory, "config.json");
        
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task SaveConfigurationAsync(CodeEditorConfiguration configuration)
    {
        try
        {
            var json = JsonSerializer.Serialize(configuration, _jsonOptions);
            await File.WriteAllTextAsync(_configPath, json);
        }
        catch (Exception ex)
        {
            // Log but don't throw - configuration persistence failure shouldn't break the application
            Console.WriteLine($"Warning: Failed to save configuration: {ex.Message}");
        }
    }

    public async Task<CodeEditorConfiguration?> LoadConfigurationAsync()
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(_configPath);
            return JsonSerializer.Deserialize<CodeEditorConfiguration>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            // Log but don't throw - fallback to default configuration
            Console.WriteLine($"Warning: Failed to load configuration: {ex.Message}");
            return null;
        }
    }

    public Task<bool> ConfigurationExistsAsync()
    {
        return Task.FromResult(File.Exists(_configPath));
    }

    public string GetConfigurationPath()
    {
        return _configPath;
    }
}
