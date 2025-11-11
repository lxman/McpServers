using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using DesktopCommander.Core.Services;
using RegistryTools;
using Mcp.Common.Core;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DesktopCommanderMcp.McpTools;

/// <summary>
/// MCP tools for Windows Registry operations
/// </summary>
[McpServerToolType]
[SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
public class RegistryTools(
    AuditLogger auditLogger,
    ILogger<RegistryTools> logger)
{
    [McpServerTool, DisplayName("read_registry_value")]
    [Description("Read a value from Windows Registry. See registry-management/read-value.md")]
    public Task<string> ReadRegistryValue(
        string path,
        string valueName)
    {
        try
        {
            using var registry = new RegistryManager(RegistryAccessMode.ReadOnly);

            if (!registry.ValueExists(path, valueName))
            {
                return Task.FromResult(JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Registry value not found: {path}\\{valueName}"
                }, SerializerOptions.JsonOptionsIndented));
            }

            RegistryValue? registryValue = registry.ReadValueWithType(path, valueName);

            if (registryValue == null)
            {
                return Task.FromResult(JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Failed to read registry value: {path}\\{valueName}"
                }, SerializerOptions.JsonOptionsIndented));
            }

            auditLogger.LogOperation("Registry_ReadValue", $"{path}\\{valueName}", success: true);

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                path,
                valueName = registryValue.Name,
                value = registryValue.Data,
                valueType = registryValue.Kind.ToString()
            }, SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading registry value: {Path}\\{ValueName}", path, valueName);
            return Task.FromResult(JsonSerializer.Serialize(
                new { success = false, error = ex.Message },
                SerializerOptions.JsonOptionsIndented));
        }
    }

    [McpServerTool, DisplayName("write_registry_value")]
    [Description("Write a value to Windows Registry. See registry-management/write-value.md")]
    public Task<string> WriteRegistryValue(
        string path,
        string valueName,
        string value,
        string valueType = "String")
    {
        try
        {
            using var registry = new RegistryManager(RegistryAccessMode.ReadWrite);

            // Parse value type
            if (!Enum.TryParse<Microsoft.Win32.RegistryValueKind>(valueType, ignoreCase: true, out var kind))
            {
                return Task.FromResult(JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Invalid value type: {valueType}. Valid types: String, DWord, QWord, Binary, MultiString, ExpandString"
                }, SerializerOptions.JsonOptionsIndented));
            }

            // Convert value based on type
            object convertedValue = kind switch
            {
                Microsoft.Win32.RegistryValueKind.DWord => int.Parse(value),
                Microsoft.Win32.RegistryValueKind.QWord => long.Parse(value),
                Microsoft.Win32.RegistryValueKind.Binary => Convert.FromHexString(value),
                Microsoft.Win32.RegistryValueKind.MultiString => value.Split('\n'),
                _ => value
            };

            registry.WriteValue(path, valueName, convertedValue, kind);

            auditLogger.LogOperation("Registry_WriteValue", $"{path}\\{valueName} = {value} ({valueType})", success: true);

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                path,
                valueName,
                value,
                valueType = kind.ToString()
            }, SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error writing registry value: {Path}\\{ValueName}", path, valueName);
            return Task.FromResult(JsonSerializer.Serialize(
                new { success = false, error = ex.Message },
                SerializerOptions.JsonOptionsIndented));
        }
    }

    [McpServerTool, DisplayName("check_registry_key_exists")]
    [Description("Check if a registry key exists. See registry-management/check-key-exists.md")]
    public Task<string> CheckRegistryKeyExists(string path)
    {
        try
        {
            using var registry = new RegistryManager(RegistryAccessMode.ReadOnly);
            bool exists = registry.KeyExists(path);

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                path,
                exists
            }, SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking registry key: {Path}", path);
            return Task.FromResult(JsonSerializer.Serialize(
                new { success = false, error = ex.Message },
                SerializerOptions.JsonOptionsIndented));
        }
    }

    [McpServerTool, DisplayName("check_registry_value_exists")]
    [Description("Check if a registry value exists. See registry-management/check-value-exists.md")]
    public Task<string> CheckRegistryValueExists(
        string path,
        string valueName)
    {
        try
        {
            using var registry = new RegistryManager(RegistryAccessMode.ReadOnly);
            bool exists = registry.ValueExists(path, valueName);

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                path,
                valueName,
                exists
            }, SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking registry value: {Path}\\{ValueName}", path, valueName);
            return Task.FromResult(JsonSerializer.Serialize(
                new { success = false, error = ex.Message },
                SerializerOptions.JsonOptionsIndented));
        }
    }

    [McpServerTool, DisplayName("get_registry_key_info")]
    [Description("Get information about a registry key including subkeys and values. See registry-management/get-key-info.md")]
    public Task<string> GetRegistryKeyInfo(string path)
    {
        try
        {
            using var registry = new RegistryManager(RegistryAccessMode.ReadOnly);

            if (!registry.KeyExists(path))
            {
                return Task.FromResult(JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Registry key not found: {path}"
                }, SerializerOptions.JsonOptionsIndented));
            }

            RegistryKeyInfo? keyInfo = registry.GetKeyInfo(path);

            if (keyInfo == null)
            {
                return Task.FromResult(JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Failed to get registry key info: {path}"
                }, SerializerOptions.JsonOptionsIndented));
            }

            auditLogger.LogOperation("Registry_GetKeyInfo", path, success: true);

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                path = keyInfo.FullPath,
                name = keyInfo.Name,
                subKeyCount = keyInfo.SubKeyCount,
                valueCount = keyInfo.ValueCount,
                subKeyNames = keyInfo.SubKeyNames,
                valueNames = keyInfo.ValueNames
            }, SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting registry key info: {Path}", path);
            return Task.FromResult(JsonSerializer.Serialize(
                new { success = false, error = ex.Message },
                SerializerOptions.JsonOptionsIndented));
        }
    }

    [McpServerTool, DisplayName("list_registry_subkeys")]
    [Description("List all subkeys under a registry key. See registry-management/list-subkeys.md")]
    public Task<string> ListRegistrySubKeys(string path)
    {
        try
        {
            using var registry = new RegistryManager(RegistryAccessMode.ReadOnly);

            if (!registry.KeyExists(path))
            {
                return Task.FromResult(JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Registry key not found: {path}"
                }, SerializerOptions.JsonOptionsIndented));
            }

            List<string> subKeys = registry.GetSubKeyNames(path);

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                path,
                subKeyCount = subKeys.Count,
                subKeys
            }, SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing registry subkeys: {Path}", path);
            return Task.FromResult(JsonSerializer.Serialize(
                new { success = false, error = ex.Message },
                SerializerOptions.JsonOptionsIndented));
        }
    }

    [McpServerTool, DisplayName("list_registry_values")]
    [Description("List all values in a registry key. See registry-management/list-values.md")]
    public Task<string> ListRegistryValues(string path)
    {
        try
        {
            using var registry = new RegistryManager(RegistryAccessMode.ReadOnly);

            if (!registry.KeyExists(path))
            {
                return Task.FromResult(JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Registry key not found: {path}"
                }, SerializerOptions.JsonOptionsIndented));
            }

            List<RegistryValue> values = registry.ReadAllValues(path);

            var valueList = values.Select(v => new
            {
                name = v.Name,
                value = v.Data,
                valueType = v.Kind.ToString()
            }).ToArray();

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                path,
                valueCount = values.Count,
                values = valueList
            }, SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing registry values: {Path}", path);
            return Task.FromResult(JsonSerializer.Serialize(
                new { success = false, error = ex.Message },
                SerializerOptions.JsonOptionsIndented));
        }
    }

    [McpServerTool, DisplayName("create_registry_key")]
    [Description("Create a new registry key. See registry-management/create-key.md")]
    public Task<string> CreateRegistryKey(string path)
    {
        try
        {
            using var registry = new RegistryManager(RegistryAccessMode.ReadWrite);

            if (registry.KeyExists(path))
            {
                return Task.FromResult(JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Registry key already exists: {path}"
                }, SerializerOptions.JsonOptionsIndented));
            }

            bool created = registry.CreateKey(path);

            if (!created)
            {
                return Task.FromResult(JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Failed to create registry key: {path}"
                }, SerializerOptions.JsonOptionsIndented));
            }

            auditLogger.LogOperation("Registry_CreateKey", path, success: true);

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                path,
                created = true
            }, SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating registry key: {Path}", path);
            return Task.FromResult(JsonSerializer.Serialize(
                new { success = false, error = ex.Message },
                SerializerOptions.JsonOptionsIndented));
        }
    }

    [McpServerTool, DisplayName("delete_registry_key")]
    [Description("Delete a registry key. See registry-management/delete-key.md")]
    public Task<string> DeleteRegistryKey(
        string path,
        bool recursive = false)
    {
        try
        {
            using var registry = new RegistryManager(RegistryAccessMode.ReadWrite);

            if (!registry.KeyExists(path))
            {
                return Task.FromResult(JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Registry key not found: {path}"
                }, SerializerOptions.JsonOptionsIndented));
            }

            registry.DeleteKey(path, recursive);

            auditLogger.LogOperation("Registry_DeleteKey", $"{path} (recursive: {recursive})", success: true);

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                path,
                deleted = true,
                recursive
            }, SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting registry key: {Path}", path);
            return Task.FromResult(JsonSerializer.Serialize(
                new { success = false, error = ex.Message },
                SerializerOptions.JsonOptionsIndented));
        }
    }

    [McpServerTool, DisplayName("delete_registry_value")]
    [Description("Delete a registry value. See registry-management/delete-value.md")]
    public Task<string> DeleteRegistryValue(
        string path,
        string valueName)
    {
        try
        {
            using var registry = new RegistryManager(RegistryAccessMode.ReadWrite);

            if (!registry.ValueExists(path, valueName))
            {
                return Task.FromResult(JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Registry value not found: {path}\\{valueName}"
                }, SerializerOptions.JsonOptionsIndented));
            }

            registry.DeleteValue(path, valueName, throwOnMissingValue: false);

            auditLogger.LogOperation("Registry_DeleteValue", $"{path}\\{valueName}", success: true);

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                path,
                valueName,
                deleted = true
            }, SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting registry value: {Path}\\{ValueName}", path, valueName);
            return Task.FromResult(JsonSerializer.Serialize(
                new { success = false, error = ex.Message },
                SerializerOptions.JsonOptionsIndented));
        }
    }

    [McpServerTool, DisplayName("enumerate_registry_keys_recursive")]
    [Description("Enumerate all subkeys recursively with optional depth limit. See registry-management/enumerate-recursive.md")]
    public Task<string> EnumerateRegistryKeysRecursive(
        string path,
        int maxDepth = 0)
    {
        try
        {
            using var registry = new RegistryManager(RegistryAccessMode.ReadOnly);

            if (!registry.KeyExists(path))
            {
                return Task.FromResult(JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Registry key not found: {path}"
                }, SerializerOptions.JsonOptionsIndented));
            }

            List<string> allKeys = registry.EnumerateSubKeysRecursive(path, maxDepth);

            auditLogger.LogOperation("Registry_EnumerateRecursive", $"{path} (depth: {maxDepth})", success: true);

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                path,
                maxDepth,
                keyCount = allKeys.Count,
                keys = allKeys
            }, SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error enumerating registry keys: {Path}", path);
            return Task.FromResult(JsonSerializer.Serialize(
                new { success = false, error = ex.Message },
                SerializerOptions.JsonOptionsIndented));
        }
    }
}