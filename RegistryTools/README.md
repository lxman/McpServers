# RegistryTools

A comprehensive .NET library for safely accessing and manipulating the Windows Registry with built-in read-only mode protection.

## Features

- **Read-Only Mode**: Configurable flag to prevent all write operations, ensuring safe registry access
- **Comprehensive Read Operations**: Check existence, read values, enumerate keys and values
- **Full Write Capabilities**: Create, update, delete, rename, and copy registry keys and values
- **Type Safety**: Proper handling of registry value types (String, DWord, QWord, Binary, etc.)
- **Exception Handling**: Custom exception for read-only violations
- **Resource Management**: Proper disposal pattern for registry key handles
- **Recursive Operations**: Support for recursive enumeration and copying

## Installation

Add the project reference to your solution or build as a NuGet package.

## Usage

### Basic Usage - Read-Only Mode (Safe)

```csharp
using RegistryTools;

// Create a read-only registry manager - no write operations allowed
using var registry = new RegistryManager(RegistryAccessMode.ReadOnly);

// Check if a key exists
bool exists = registry.KeyExists(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft");

// Read a value
var value = registry.ReadValue(@"HKEY_CURRENT_USER\Software\MyApp", "Version");

// Get detailed information about a key
var keyInfo = registry.GetKeyInfo(@"HKEY_LOCAL_MACHINE\SOFTWARE");
Console.WriteLine($"Subkeys: {keyInfo.SubKeyCount}, Values: {keyInfo.ValueCount}");

// Read all values from a key
var allValues = registry.ReadAllValues(@"HKEY_CURRENT_USER\Software\MyApp");
foreach (var val in allValues)
{
    Console.WriteLine($"{val.Name} ({val.Kind}): {val.Data}");
}

// Any write operation will throw ReadOnlyRegistryException
try
{
    registry.WriteValue(@"HKEY_CURRENT_USER\Software\MyApp", "Test", "Value");
}
catch (ReadOnlyRegistryException ex)
{
    Console.WriteLine("Write operation blocked: " + ex.Message);
}
```

### Read-Write Mode

```csharp
using RegistryTools;

// Create a read-write registry manager
using var registry = new RegistryManager(RegistryAccessMode.ReadWrite);

// Create a new key
registry.CreateKey(@"HKEY_CURRENT_USER\Software\MyApp\Settings");

// Write values of different types
registry.WriteValue(@"HKEY_CURRENT_USER\Software\MyApp", "AppName", "MyApplication", RegistryValueKind.String);
registry.WriteValue(@"HKEY_CURRENT_USER\Software\MyApp", "Version", 1, RegistryValueKind.DWord);
registry.WriteValue(@"HKEY_CURRENT_USER\Software\MyApp", "InstallDate", DateTime.Now.ToString(), RegistryValueKind.String);

// Read a value with type information
var valueWithType = registry.ReadValueWithType(@"HKEY_CURRENT_USER\Software\MyApp", "Version");
Console.WriteLine($"Value: {valueWithType.Data}, Type: {valueWithType.Kind}");

// Rename a value
registry.RenameValue(@"HKEY_CURRENT_USER\Software\MyApp", "Version", "AppVersion");

// Copy a key and all its contents
registry.CopyKey(
    @"HKEY_CURRENT_USER\Software\MyApp",
    @"HKEY_CURRENT_USER\Software\MyApp_Backup",
    recursive: true
);

// Delete a value
registry.DeleteValue(@"HKEY_CURRENT_USER\Software\MyApp", "TempValue");

// Delete a key (recursive will delete all subkeys)
registry.DeleteKey(@"HKEY_CURRENT_USER\Software\MyApp_Backup", recursive: true);
```

### Enumerating Registry Keys

```csharp
using var registry = new RegistryManager(RegistryAccessMode.ReadOnly);

// Get immediate subkeys
var subKeys = registry.GetSubKeyNames(@"HKEY_LOCAL_MACHINE\SOFTWARE");
foreach (var subKey in subKeys)
{
    Console.WriteLine(subKey);
}

// Recursively enumerate all subkeys with depth limit
var allSubKeys = registry.EnumerateSubKeysRecursive(
    @"HKEY_CURRENT_USER\Software\Microsoft",
    maxDepth: 3  // Limit recursion depth (0 = unlimited)
);

Console.WriteLine($"Found {allSubKeys.Count} subkeys");
```

### Checking Existence

```csharp
using var registry = new RegistryManager(RegistryAccessMode.ReadOnly);

// Check if a key exists
if (registry.KeyExists(@"HKEY_CURRENT_USER\Software\MyApp"))
{
    Console.WriteLine("Key exists");
}

// Check if a specific value exists
if (registry.ValueExists(@"HKEY_CURRENT_USER\Software\MyApp", "Version"))
{
    Console.WriteLine("Value exists");
}
```

### Getting Key Information

```csharp
using var registry = new RegistryManager(RegistryAccessMode.ReadOnly);

var info = registry.GetKeyInfo(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft");
if (!(info is null))
{
    Console.WriteLine($"Full Path: {info.FullPath}");
    Console.WriteLine($"Subkey Count: {info.SubKeyCount}");
    Console.WriteLine($"Value Count: {info.ValueCount}");
    
    Console.WriteLine("Subkeys:");
    foreach (var subKey in info.SubKeyNames)
    {
        Console.WriteLine($"  - {subKey}");
    }
    
    Console.WriteLine("Values:");
    foreach (var valueName in info.ValueNames)
    {
        Console.WriteLine($"  - {valueName}");
    }
}
```

## Registry Value Types

The library supports all standard Windows Registry value types:

- `RegistryValueKind.String` - Text string
- `RegistryValueKind.DWord` - 32-bit number
- `RegistryValueKind.QWord` - 64-bit number
- `RegistryValueKind.Binary` - Binary data
- `RegistryValueKind.MultiString` - Array of strings
- `RegistryValueKind.ExpandString` - String with environment variables
- `RegistryValueKind.None` - No type

## Supported Registry Hives

The library supports all standard Windows Registry hives with common abbreviations:

- `HKEY_CLASSES_ROOT` or `HKCR`
- `HKEY_CURRENT_USER` or `HKCU`
- `HKEY_LOCAL_MACHINE` or `HKLM`
- `HKEY_USERS` or `HKU`
- `HKEY_CURRENT_CONFIG` or `HKCC`

## Safety Features

### Read-Only Mode

The primary safety feature is the `RegistryAccessMode.ReadOnly` mode which:

1. Prevents all write operations (Create, Write, Delete, Rename, Copy)
2. Throws `ReadOnlyRegistryException` if write operations are attempted
3. Allows safe exploration and reading of registry data
4. Can be used for auditing, monitoring, or diagnostic tools

```csharp
// Safe for production monitoring tools
using var registry = new RegistryManager(RegistryAccessMode.ReadOnly);

// All read operations work normally
var data = registry.ReadValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\MyApp", "Config");

// All write operations are blocked
// registry.WriteValue(...);  // Throws ReadOnlyRegistryException
```

### Access Mode Property

You can check the current access mode at runtime:

```csharp
using var registry = new RegistryManager(RegistryAccessMode.ReadOnly);

if (registry.IsReadOnly)
{
    Console.WriteLine("Registry is in read-only mode");
}

Console.WriteLine($"Access Mode: {registry.AccessMode}");
```

## Error Handling

The library provides specific exceptions for different scenarios:

```csharp
try
{
    using var registry = new RegistryManager(RegistryAccessMode.ReadWrite);
    registry.WriteValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Protected", "Value", "Data");
}
catch (ReadOnlyRegistryException ex)
{
    // Attempted write in read-only mode
    Console.WriteLine($"Read-only violation: {ex.Message}");
}
catch (UnauthorizedAccessException ex)
{
    // Insufficient permissions
    Console.WriteLine($"Access denied: {ex.Message}");
}
catch (ArgumentException ex)
{
    // Invalid path or key not found
    Console.WriteLine($"Invalid argument: {ex.Message}");
}
```

## Best Practices

1. **Use Read-Only Mode by Default**: When you only need to read registry data, always use `RegistryAccessMode.ReadOnly`
2. **Dispose Properly**: Always use `using` statements or explicitly dispose the `RegistryManager`
3. **Check Existence**: Use `KeyExists()` and `ValueExists()` before reading or deleting
4. **Handle Exceptions**: Registry operations can fail due to permissions or missing keys
5. **Limit Recursion Depth**: When enumerating recursively, use `maxDepth` to avoid performance issues
6. **Validate Paths**: Ensure registry paths are properly formatted before operations

## Requirements

- .NET Standard 2.1 or higher
- Windows operating system
- Microsoft.Win32.Registry NuGet package

## License

[Your License Here]

## Contributing

[Your Contributing Guidelines Here]
