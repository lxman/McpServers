# Mcp.Common.Core

Common utilities, extensions, and patterns used across all MCP (Model Context Protocol) servers.

## Purpose

This library provides foundational utilities that eliminate code duplication across MCP server implementations. It consolidates common patterns for JSON serialization, date/time operations, string manipulation, and other frequently-used functionality.

## Contents

### SerializerOptions

Provides standardized JSON serializer options for consistent formatting across all MCP servers.

```csharp
using Mcp.Common;

// Indented JSON for human-readable output
var indented = SerializerOptions.JsonOptionsIndented;

// Compact JSON for minimal payload size
var compact = SerializerOptions.JsonOptionsCompact;
```

### DateTimeExtensions

Extension methods for DateTime operations including formatting, relative time display, and range checking.

```csharp
using Mcp.Common.Extensions;

// ISO 8601 formatting
string iso = DateTime.UtcNow.ToIso8601Format();
// Output: "2025-11-09T10:30:45.123Z"

// Human-readable relative time
string ago = someDate.ToTimeAgo();
// Output: "2 hours ago"

// Date range checking
bool recent = someDate.IsWithinLastDays(7);

// Simple date format
string simple = DateTime.Now.ToSimpleDateFormat();
// Output: "2025-11-09"
```

**Available Methods:**
- `ToIso8601Format()` - ISO 8601 format with milliseconds
- `ToTimeAgo()` - Human-readable relative time (e.g., "3 hours ago")
- `IsWithinLastDays(int days)` - Check if within specified day range
- `IsWithinLastHours(int hours)` - Check if within specified hour range
- `ToSimpleDateFormat()` - Simple yyyy-MM-dd format
- `IsToday()` - Check if date is today (UTC)

### StringExtensions

Extension methods for string operations including validation, formatting, and transformation.

```csharp
using Mcp.Common.Extensions;

// Null/empty checking
if (someString.IsNullOrWhiteSpace())
{
    // Handle empty string
}

// Default value fallback
string display = userName.OrDefault("Anonymous");

// Truncation with ellipsis
string preview = longText.Truncate(50);
// Output: "This is a very long text that will be trun..."

// Email extraction
string email = "John Doe <john@example.com>".ExtractEmail();
// Output: "john@example.com"

string name = "John Doe <john@example.com>".ExtractDisplayName();
// Output: "John Doe"

// Safe filename conversion
string filename = "Invalid/File:Name*.txt".ToSafeFileName();
// Output: "InvalidFileName.txt"

// Ensure prefix/suffix
string url = "/api/users".EnsureStartsWith("/");
string path = "myfile".EnsureEndsWith(".txt");
```

**Available Methods:**
- `IsNullOrEmpty()` - Check if null or empty
- `IsNullOrWhiteSpace()` - Check if null, empty, or whitespace
- `OrDefault(string defaultValue)` - Return default if null/empty
- `Truncate(int maxLength, bool addEllipsis)` - Truncate with optional ellipsis
- `ExtractDisplayName()` - Extract name from "Name &lt;email&gt;" format
- `ExtractEmail()` - Extract email from "Name &lt;email&gt;" format
- `ToSafeFileName()` - Remove invalid filename characters
- `ToTitleCase()` - Convert to title case
- `EnsureStartsWith(string prefix)` - Ensure string starts with prefix
- `EnsureEndsWith(string suffix)` - Ensure string ends with suffix

## Installation

### From Project Reference

Add to your MCP server's `.csproj`:

```xml
<ItemGroup>
    <ProjectReference Include="..\Libraries\Mcp.Common.Core\Mcp.Common.Core.csproj" />
</ItemGroup>
```

### Using in Code

```csharp
// Import namespace for utilities
using Mcp.Common;

// Import namespace for extensions
using Mcp.Common.Extensions;
```

## Dependencies

- **System.Text.Json** 9.0.0 - JSON serialization
- **Microsoft.Extensions.Logging.Abstractions** 10.0.0-rc.2 - Logging abstractions

## Target Framework

- .NET 9.0

## Migration from Existing Code

If your server core library currently has duplicate versions of these utilities:

### Replacing SerializerOptions

**Before:**
```csharp
using YourServer.Core.Common;

var options = SerializerOptions.JsonOptionsIndented;
```

**After:**
```csharp
using Mcp.Common;

var options = SerializerOptions.JsonOptionsIndented;
```

### Replacing Extensions

**Before:**
```csharp
using AzureServer.Core.Common.Extensions;

string formatted = date.ToDevOpsFormat();
```

**After:**
```csharp
using Mcp.Common.Extensions;

string formatted = date.ToIso8601Format();
```

## Consolidation Impact

This library consolidates code from:
- AwsServer.Core/Common/SerializerOptions.cs
- AzureServer.Core/Common/SerializerOptions.cs
- CSharpAnalyzer.Core/Models/SerializerOptions.cs
- DebugServer.Core/Common/SerializerOptions.cs
- DesktopCommander.Core/Common/SerializerOptions.cs
- DocumentServer.Core/Common/SerializerOptions.cs
- MongoServer.Core/Common/SerializerOptions.cs
- SqlServer.Core/Common/SerializerOptions.cs
- AzureServer.Core/Common/Extensions/DateTimeExtensions.cs
- AzureServer.Core/Common/Extensions/StringExtensions.cs

**Total lines eliminated:** ~800+ lines of duplicate code

## Design Principles

1. **Zero External Dependencies** - Only depends on core .NET libraries
2. **Extension Method Pattern** - Utilities exposed as extension methods for natural syntax
3. **Nullable-Aware** - Full support for nullable reference types
4. **Well-Documented** - XML documentation on all public members
5. **Performance-Conscious** - No allocations in hot paths

## Future Additions

Potential future additions based on common patterns:
- Collection extensions (e.g., `IsNullOrEmpty()` for IEnumerable)
- Exception types for MCP operations
- Result/Response wrapper types
- Validation utilities
- Retry policy helpers

## Contributing

When adding new utilities to this library:

1. **Ensure Wide Applicability** - Utility must be useful to 3+ MCP servers
2. **Document Thoroughly** - Add XML docs and usage examples
3. **Test Extensively** - Add unit tests for all code paths
4. **Keep It Simple** - Avoid complex abstractions; favor clarity
5. **Maintain Backward Compatibility** - Don't break existing consumers

## Version History

- **1.0.0** (2025-11-09)
  - Initial release
  - SerializerOptions with indented and compact options
  - DateTimeExtensions with formatting and relative time
  - StringExtensions with validation and transformation

## License

Part of the McpServers project. See root LICENSE file for details.
