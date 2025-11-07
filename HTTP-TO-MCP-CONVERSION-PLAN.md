# HTTP to MCP Server Conversion Plan

## Overview
Converting HTTP API servers back to MCP STDIO servers using the Agent Skills documentation pattern for token conservation.

## Motivation
- **Original Issue**: MCP STDIO servers front-loaded all tool documentation, consuming excessive tokens
- **HTTP Solution**: Lazy-loading via HTTP servers - only start and inspect when needed
- **New Solution**: Agent Skills with hierarchical markdown documentation - minimal tool descriptions with deferred documentation loading

## Architecture Pattern

### Core Libraries (Already Complete)
- Business logic, services, models
- No protocol-specific code
- Reusable by any interface type
- Examples: AwsServer.Core, AzureServer.Core, CSharpAnalyzer.Core

### MCP STDIO Servers
- Thin wrapper around Core libraries
- McpTools classes for MCP protocol interface
- Minimal tool descriptions with documentation references
- File-based logging (stdout/stdin reserved for MCP protocol)

## Documentation Structure

### Skills Folder Organization
```
skills/
├── INDEX.md              # Central navigation/directory
├── desktop-commander/    # Existing pattern reference
├── sql-mcp/             # Existing pattern reference
├── aws/                 # AWS services documentation
│   ├── common/          # Shared concepts
│   │   ├── auth.md      # Authentication patterns
│   │   ├── errors.md    # Common error responses
│   │   └── pagination.md # Pagination patterns
│   ├── s3/              # S3-specific docs
│   ├── ec2/             # EC2-specific docs
│   └── ...
├── azure/               # Azure services documentation
├── csharp-analyzer/     # C# analysis documentation
└── ...
```

### Tool Description Format
**Standard format**: `"Brief operation description. See skills/category/tool.md only when using this tool"`

**Example**:
```json
{
  "name": "list_s3_buckets",
  "description": "List S3 buckets. See skills/aws/s3/list-buckets.md only when using this tool"
}
```

### Documentation Content Strategy
- **Common files**: Authentication, errors, pagination, shared patterns
- **Tool-specific files**: Minimal content, reference common files
- **Token optimization**: Every word counts, avoid redundancy

### Build Configuration for Skills Documentation
To ensure documentation is available at runtime, add to `.csproj`:
```xml
<ItemGroup>
  <!-- Copy skills documentation to output directory -->
  <None Update="skills\**\*.md">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```
This copies the skills folder to the output directory (e.g., `bin/Debug/net9.0/skills/`), making documentation accessible relative to the executing assembly.

## Logging Strategy for STDIO Servers

**Constraints**:
- stdout/stdin reserved exclusively for MCP protocol
- No Console.WriteLine() permitted
- All logging must go elsewhere

**Solution**:
```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File(
        path: "logs/servername-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.Console(
        standardErrorFromLevel: LogEventLevel.Warning) // Only warnings/errors to stderr
    .CreateLogger();
```

## Conversion Process

### For Each HTTP Server:
1. **Create new MCP project** (e.g., AwsMcp)
2. **Reference Core library** (e.g., AwsServer.Core)
3. **Create McpTools classes** wrapping Core services
4. **Implement minimal tool descriptions**
5. **Create documentation hierarchy** in skills folder
6. **Update skills/INDEX.md**
7. **Configure file-based logging**
8. **Test MCP communication**

### Servers to Convert:
1. AwsServer → AwsMcp
2. AzureServer → AzureMcp
3. CSharpAnalyzer → CSharpAnalyzerMcp

## Implementation Details

### MCP Tool Implementation Pattern
```csharp
[Tool("list_s3_buckets", "List S3 buckets. See skills/aws/s3/list-buckets.md only when using this tool")]
public async Task<object> ListS3Buckets(/* params */)
{
    // Delegate to Core service
    return await s3Service.ListBucketsAsync();
}
```

### Service Registration Pattern
```csharp
builder.Services.AddSingleton<S3Service>();
builder.Services.AddSingleton<EC2Service>();
// ... other Core services

builder.Services.AddSingleton<S3Tools>();
builder.Services.AddSingleton<EC2Tools>();
// ... other MCP tools
```

## Token Optimization Strategies

1. **Minimal descriptions**: ~40-50 chars per tool
2. **Deferred loading**: Documentation only read when tool is used
3. **Common patterns**: Shared documentation reduces redundancy
4. **Hierarchical structure**: Progressive disclosure of details
5. **Consistent format**: Predictable pattern reduces cognitive load

## Success Metrics

- **Token reduction**: 90%+ reduction in upfront token cost
- **Functionality preserved**: All HTTP server capabilities available
- **Documentation quality**: Clear, concise, complete
- **Performance**: Fast MCP communication, efficient service calls
- **Maintainability**: Clean separation of concerns, easy to extend

## Next Steps

1. ✅ Create planning document (this file)
2. 🚧 Convert AwsServer to AwsMcp
3. ⏳ Convert AzureServer to AzureMcp
4. ⏳ Convert CSharpAnalyzer to CSharpAnalyzerMcp
5. ⏳ Update skills/INDEX.md with all services
6. ⏳ Validate token optimization achieved
7. ⏳ Document any patterns for future conversions