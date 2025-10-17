using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;

namespace RegistryTools
{
    /// <summary>
    /// Provides safe access to the Windows Registry with configurable read-only mode.
    /// </summary>
    public class RegistryManager : IDisposable
    {
        private readonly RegistryAccessMode _accessMode;
        private readonly Dictionary<string, RegistryKey> _openKeys = new Dictionary<string, RegistryKey>();
        private bool _disposed;

        /// <summary>
        /// Gets the current access mode (ReadOnly or ReadWrite).
        /// </summary>
        public RegistryAccessMode AccessMode => _accessMode;

        /// <summary>
        /// Gets whether write operations are allowed.
        /// </summary>
        public bool IsReadOnly => _accessMode == RegistryAccessMode.ReadOnly;

        /// <summary>
        /// Initializes a new instance of the RegistryManager class.
        /// </summary>
        /// <param name="accessMode">The access mode for registry operations.</param>
        public RegistryManager(RegistryAccessMode accessMode = RegistryAccessMode.ReadWrite)
        {
            _accessMode = accessMode;
        }

        #region Helper Methods

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(RegistryManager));
            }
        }

        private void EnsureWriteAccess()
        {
            if (IsReadOnly)
            {
                throw new ReadOnlyRegistryException();
            }
        }

        private RegistryKey GetBaseKey(string hive)
        {
            return hive.ToUpperInvariant() switch
            {
                "HKEY_CLASSES_ROOT" => Registry.ClassesRoot,
                "HKCR" => Registry.ClassesRoot,
                "HKEY_CURRENT_USER" => Registry.CurrentUser,
                "HKCU" => Registry.CurrentUser,
                "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
                "HKLM" => Registry.LocalMachine,
                "HKEY_USERS" => Registry.Users,
                "HKU" => Registry.Users,
                "HKEY_CURRENT_CONFIG" => Registry.CurrentConfig,
                "HKCC" => Registry.CurrentConfig,
                _ => throw new ArgumentException($"Unknown registry hive: {hive}", nameof(hive))
            };
        }

        private (RegistryKey baseKey, string subKeyPath) ParseRegistryPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Registry path cannot be null or empty.", nameof(path));
            }

            string[] parts = path.Split(new[] { '\\' }, 2);
            if (parts.Length == 0)
            {
                throw new ArgumentException("Invalid registry path format.", nameof(path));
            }

            RegistryKey baseKey = GetBaseKey(parts[0]);
            string subKeyPath = parts.Length > 1 ? parts[1] : string.Empty;

            return (baseKey, subKeyPath);
        }

        #endregion

        #region Read Operations

        /// <summary>
        /// Checks if a registry key exists.
        /// </summary>
        /// <param name="path">Full registry path (e.g., "HKEY_LOCAL_MACHINE\\SOFTWARE\\MyApp").</param>
        /// <returns>True if the key exists, false otherwise.</returns>
        public bool KeyExists(string path)
        {
            EnsureNotDisposed();

            try
            {
                (RegistryKey baseKey, string subKeyPath) = ParseRegistryPath(path);
            
                if (string.IsNullOrEmpty(subKeyPath))
                {
                    return true; // Base keys always exist
                }

                using RegistryKey? key = baseKey.OpenSubKey(subKeyPath, writable: false);
                return !(key is null);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if a registry value exists.
        /// </summary>
        /// <param name="path">Full registry key path.</param>
        /// <param name="valueName">Name of the value to check.</param>
        /// <returns>True if the value exists, false otherwise.</returns>
        public bool ValueExists(string path, string valueName)
        {
            EnsureNotDisposed();

            try
            {
                (RegistryKey baseKey, string subKeyPath) = ParseRegistryPath(path);
                using RegistryKey? key = baseKey.OpenSubKey(subKeyPath, writable: false);
            
                if (key is null)
                {
                    return false;
                }

                string[] valueNames = key.GetValueNames();
                return valueNames.Contains(valueName);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets information about a registry key.
        /// </summary>
        /// <param name="path">Full registry key path.</param>
        /// <returns>RegistryKeyInfo object containing key metadata.</returns>
        public RegistryKeyInfo? GetKeyInfo(string path)
        {
            EnsureNotDisposed();

            try
            {
                (RegistryKey baseKey, string subKeyPath) = ParseRegistryPath(path);
                using RegistryKey? key = baseKey.OpenSubKey(subKeyPath, writable: false);
            
                if (key is null)
                {
                    return null;
                }

                var info = new RegistryKeyInfo
                {
                    FullPath = path,
                    Name = key.Name,
                    SubKeyCount = key.SubKeyCount,
                    ValueCount = key.ValueCount,
                    SubKeyNames = key.GetSubKeyNames().ToList(),
                    ValueNames = key.GetValueNames().ToList()
                };

                return info;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the names of all subkeys under the specified key.
        /// </summary>
        /// <param name="path">Full registry key path.</param>
        /// <returns>List of subkey names.</returns>
        public List<string> GetSubKeyNames(string path)
        {
            EnsureNotDisposed();

            (RegistryKey baseKey, string subKeyPath) = ParseRegistryPath(path);
            using RegistryKey? key = baseKey.OpenSubKey(subKeyPath, writable: false);
        
            if (key is null)
            {
                throw new ArgumentException($"Registry key not found: {path}", nameof(path));
            }

            return key.GetSubKeyNames().ToList();
        }

        /// <summary>
        /// Gets the names of all values under the specified key.
        /// </summary>
        /// <param name="path">Full registry key path.</param>
        /// <returns>List of value names.</returns>
        public List<string> GetValueNames(string path)
        {
            EnsureNotDisposed();

            (RegistryKey baseKey, string subKeyPath) = ParseRegistryPath(path);
            using RegistryKey? key = baseKey.OpenSubKey(subKeyPath, writable: false);
        
            if (key is null)
            {
                throw new ArgumentException($"Registry key not found: {path}", nameof(path));
            }

            return key.GetValueNames().ToList();
        }

        /// <summary>
        /// Reads a registry value.
        /// </summary>
        /// <param name="path">Full registry key path.</param>
        /// <param name="valueName">Name of the value to read.</param>
        /// <returns>The value data, or null if not found.</returns>
        public object? ReadValue(string path, string valueName)
        {
            EnsureNotDisposed();

            (RegistryKey baseKey, string subKeyPath) = ParseRegistryPath(path);
            using RegistryKey? key = baseKey.OpenSubKey(subKeyPath, writable: false);
        
            if (key is null)
            {
                return null;
            }

            return key.GetValue(valueName);
        }

        /// <summary>
        /// Reads a registry value with its type information.
        /// </summary>
        /// <param name="path">Full registry key path.</param>
        /// <param name="valueName">Name of the value to read.</param>
        /// <returns>RegistryValue object containing the value data and type, or null if not found.</returns>
        public RegistryValue? ReadValueWithType(string path, string valueName)
        {
            EnsureNotDisposed();

            (RegistryKey baseKey, string subKeyPath) = ParseRegistryPath(path);
            using RegistryKey? key = baseKey.OpenSubKey(subKeyPath, writable: false);
        
            if (key is null)
            {
                return null;
            }

            object? data = key.GetValue(valueName);
            if (data is null)
            {
                return null;
            }

            RegistryValueKind kind = key.GetValueKind(valueName);
            return new RegistryValue(valueName, data, kind);
        }

        /// <summary>
        /// Reads all values from a registry key.
        /// </summary>
        /// <param name="path">Full registry key path.</param>
        /// <returns>List of RegistryValue objects.</returns>
        public List<RegistryValue> ReadAllValues(string path)
        {
            EnsureNotDisposed();

            (RegistryKey baseKey, string subKeyPath) = ParseRegistryPath(path);
            using RegistryKey? key = baseKey.OpenSubKey(subKeyPath, writable: false);
        
            if (key is null)
            {
                throw new ArgumentException($"Registry key not found: {path}", nameof(path));
            }

            var values = new List<RegistryValue>();
            foreach (string valueName in key.GetValueNames())
            {
                object? data = key.GetValue(valueName);
                RegistryValueKind kind = key.GetValueKind(valueName);
                values.Add(new RegistryValue(valueName, data, kind));
            }

            return values;
        }

        /// <summary>
        /// Enumerates all subkeys recursively with optional depth limit.
        /// </summary>
        /// <param name="path">Full registry key path.</param>
        /// <param name="maxDepth">Maximum recursion depth (0 = unlimited).</param>
        /// <returns>List of all subkey paths.</returns>
        public List<string> EnumerateSubKeysRecursive(string path, int maxDepth = 0)
        {
            EnsureNotDisposed();

            var result = new List<string>();
            EnumerateSubKeysRecursiveInternal(path, result, 0, maxDepth);
            return result;
        }

        private void EnumerateSubKeysRecursiveInternal(string path, List<string> result, int currentDepth, int maxDepth)
        {
            if (maxDepth > 0 && currentDepth >= maxDepth)
            {
                return;
            }

            try
            {
                List<string> subKeys = GetSubKeyNames(path);
                foreach (string? subKey in subKeys)
                {
                    var fullPath = $"{path}\\{subKey}";
                    result.Add(fullPath);
                    EnumerateSubKeysRecursiveInternal(fullPath, result, currentDepth + 1, maxDepth);
                }
            }
            catch
            {
                // Skip keys that can't be accessed
            }
        }

        #endregion

        #region Write Operations

        /// <summary>
        /// Creates a new registry key.
        /// </summary>
        /// <param name="path">Full registry key path to create.</param>
        /// <returns>True if created successfully.</returns>
        public bool CreateKey(string path)
        {
            EnsureNotDisposed();
            EnsureWriteAccess();

            try
            {
                (RegistryKey baseKey, string subKeyPath) = ParseRegistryPath(path);
                using RegistryKey key = baseKey.CreateSubKey(subKeyPath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Writes a value to the registry.
        /// </summary>
        /// <param name="path">Full registry key path.</param>
        /// <param name="valueName">Name of the value to write.</param>
        /// <param name="value">Data to write.</param>
        /// <param name="valueKind">Type of registry value.</param>
        public void WriteValue(string path, string valueName, object value, RegistryValueKind valueKind = RegistryValueKind.String)
        {
            EnsureNotDisposed();
            EnsureWriteAccess();

            (RegistryKey baseKey, string subKeyPath) = ParseRegistryPath(path);
            using RegistryKey? key = baseKey.CreateSubKey(subKeyPath, writable: true);
        
            if (key is null)
            {
                throw new InvalidOperationException($"Failed to create or open registry key: {path}");
            }

            key.SetValue(valueName, value, valueKind);
        }

        /// <summary>
        /// Deletes a registry value.
        /// </summary>
        /// <param name="path">Full registry key path.</param>
        /// <param name="valueName">Name of the value to delete.</param>
        /// <param name="throwOnMissingValue">Whether to throw if the value doesn't exist.</param>
        public void DeleteValue(string path, string valueName, bool throwOnMissingValue = false)
        {
            EnsureNotDisposed();
            EnsureWriteAccess();

            (RegistryKey baseKey, string subKeyPath) = ParseRegistryPath(path);
            using RegistryKey? key = baseKey.OpenSubKey(subKeyPath, writable: true);
        
            if (key is null)
            {
                if (throwOnMissingValue)
                {
                    throw new ArgumentException($"Registry key not found: {path}", nameof(path));
                }
                return;
            }

            key.DeleteValue(valueName, throwOnMissingValue);
        }

        /// <summary>
        /// Deletes a registry key and optionally all its subkeys.
        /// </summary>
        /// <param name="path">Full registry key path to delete.</param>
        /// <param name="recursive">Whether to delete subkeys recursively.</param>
        public void DeleteKey(string path, bool recursive = false)
        {
            EnsureNotDisposed();
            EnsureWriteAccess();

            (RegistryKey baseKey, string subKeyPath) = ParseRegistryPath(path);
        
            if (string.IsNullOrEmpty(subKeyPath))
            {
                throw new ArgumentException("Cannot delete a base registry hive.", nameof(path));
            }

            if (recursive)
            {
                baseKey.DeleteSubKeyTree(subKeyPath, throwOnMissingSubKey: false);
            }
            else
            {
                baseKey.DeleteSubKey(subKeyPath, throwOnMissingSubKey: false);
            }
        }

        /// <summary>
        /// Renames a registry value.
        /// </summary>
        /// <param name="path">Full registry key path.</param>
        /// <param name="oldValueName">Current name of the value.</param>
        /// <param name="newValueName">New name for the value.</param>
        public void RenameValue(string path, string oldValueName, string newValueName)
        {
            EnsureNotDisposed();
            EnsureWriteAccess();

            RegistryValue? valueWithType = ReadValueWithType(path, oldValueName);
            if (valueWithType is null)
            {
                throw new ArgumentException($"Value '{oldValueName}' not found in key: {path}");
            }

            WriteValue(path, newValueName, valueWithType.Data!, valueWithType.Kind);
            DeleteValue(path, oldValueName);
        }

        /// <summary>
        /// Copies a registry key and all its contents to a new location.
        /// </summary>
        /// <param name="sourcePath">Source registry key path.</param>
        /// <param name="destinationPath">Destination registry key path.</param>
        /// <param name="recursive">Whether to copy subkeys recursively.</param>
        public void CopyKey(string sourcePath, string destinationPath, bool recursive = true)
        {
            EnsureNotDisposed();
            EnsureWriteAccess();

            if (!KeyExists(sourcePath))
            {
                throw new ArgumentException($"Source key not found: {sourcePath}", nameof(sourcePath));
            }

            // Create destination key
            CreateKey(destinationPath);

            // Copy all values
            List<RegistryValue> values = ReadAllValues(sourcePath);
            foreach (RegistryValue? value in values)
            {
                WriteValue(destinationPath, value.Name, value.Data!, value.Kind);
            }

            // Copy subkeys if recursive
            if (recursive)
            {
                List<string> subKeys = GetSubKeyNames(sourcePath);
                foreach (string? subKey in subKeys)
                {
                    var sourceSubKeyPath = $"{sourcePath}\\{subKey}";
                    var destSubKeyPath = $"{destinationPath}\\{subKey}";
                    CopyKey(sourceSubKeyPath, destSubKeyPath, recursive: true);
                }
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                foreach (RegistryKey? key in _openKeys.Values)
                {
                    key?.Dispose();
                }
                _openKeys.Clear();
            }

            _disposed = true;
        }

        #endregion
    }
}
