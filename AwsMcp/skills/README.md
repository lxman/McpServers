# Skills Documentation System

## Overview
The skills folder contains documentation for MCP tools that is loaded on-demand to conserve tokens. Each tool references its documentation with the pattern: "See skills/service/tool.md only when using this tool"

## Build Configuration
The `.csproj` file is configured to automatically copy all markdown files from the skills folder to the output directory during build:

```xml
<ItemGroup>
  <None Update="skills\**\*.md">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

## Directory Structure
When the application is compiled and run, the skills folder will be copied to the output directory:
- Source: `AwsMcp/skills/`
- Output: `AwsMcp/bin/Debug/net9.0/skills/`

## Runtime Access
The AI agent can access documentation files relative to the executing assembly:
- Path from DLL: `skills/aws/service/operation.md`

## Adding New Documentation
1. Create markdown files in the appropriate service folder
2. Follow the naming convention: `operation-name.md`
3. Include:
   - Brief description
   - Parameters with types
   - Return value format
   - Example usage
   - Any warnings or notes

## Benefits
- **Token Conservation**: Documentation is only loaded when a tool is actually used
- **Maintainability**: Documentation is separate from code
- **Flexibility**: Documentation can be updated without recompiling
- **Discoverability**: Clear hierarchy makes it easy to find relevant docs