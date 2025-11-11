# Shared Libraries Consolidation Plan

## Overview

This document tracks the consolidation of common capabilities across MCP servers into shared libraries to eliminate duplication, improve maintainability, and establish consistent patterns.

## Current State Analysis

### Existing Shared Libraries (Working Well)

| Library | Used By | Purpose |
|---------|---------|---------|
| **SerilogFileWriter** | 12 servers | MCP-aware logging with file output |
| **RegistryTools** | 4 cores (AWS, Azure, Mongo, Redis) | Windows registry access patterns |
| **MsOfficeCrypto** | DocumentServer.Core | Office file encryption/decryption |

### Server-Specific Core Libraries (14 Total)

1. AwsServer.Core
2. AzureServer.Core
3. CSharpAnalyzer.Core
4. DebugServer.Core
5. DesktopCommander.Core
6. DocumentServer.Core
7. MongoServer.Core
8. Playwright.Core
9. RedisBrowser.Core
10. SeleniumChrome.Core
11. SqlServer.Core
12. (3 additional analyzer cores)

### Critical Duplications Identified

#### 1. SerializerOptions.cs
**Duplicated in 8 libraries:** AwsServer.Core, AzureServer.Core, CSharpAnalyzer.Core, DebugServer.Core, DesktopCommander.Core, DocumentServer.Core, MongoServer.Core, SqlServer.Core

**Code Pattern:**
```csharp
public static class SerializerOptions
{
    public static JsonSerializerOptions JsonOptionsIndented => new() { WriteIndented = true };
}
```

**Impact:** ~200 lines of identical code across namespaces

#### 2. ServiceCollectionExtensions.cs
**Found in:** AwsServer.Core/Configuration, AzureServer.Core/Configuration

**Impact:** ~500+ lines of duplicate DI setup patterns

#### 3. Common Extensions
**Found in:** AzureServer.Core/Common/Extensions
- DateTimeExtensions.cs
- StringExtensions.cs

**Impact:** ~500 lines of utility methods that could benefit other servers

#### 4. HTTP Client Patterns
**Duplicated across:** AwsServer.Core, AzureServer.Core, Playwright.Core, SeleniumChrome.Core

**Impact:** ~800+ lines of HTTP client configuration and retry logic

#### 5. Database Connection Management
**Duplicated across:** MongoServer.Core, SeleniumChrome.Core (MongoDB), RedisBrowser.Core, SqlServer.Core

**Impact:** ~1000+ lines of connection pooling, retry policies, health checks

## Proposed Shared Library Architecture

### 1. Mcp.Common.Core (Foundation Library)

**Purpose:** Core utilities and extensions used across all MCP servers

**Contents:**
- `SerializerOptions` - Consolidated from 8 libraries
- `DateTimeExtensions` - Date/time utility methods
- `StringExtensions` - String manipulation utilities
- `McpException` - Base exception type for MCP operations
- `McpConstants` - Common constants across servers
- Common interfaces for MCP tool patterns
- Result/Response wrapper types

**Dependencies:**
```xml
<PackageReference Include="System.Text.Json" Version="9.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.0" />
```

**Namespace:** `Mcp.Common`

**Estimated Size:** 500-800 lines

---

### 2. Mcp.DependencyInjection.Core

**Purpose:** Common dependency injection patterns and service registration

**Contents:**
- `ServiceCollectionExtensions` - Consolidated from AWS/Azure
- Configuration binding utilities
- Health check registration helpers
- Logging service registration patterns
- Common service lifetime patterns

**Dependencies:**
```xml
<ProjectReference Include="..\Mcp.Common.Core\Mcp.Common.Core.csproj" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.0" />
<PackageReference Include="Microsoft.Extensions.Configuration" Version="10.0.0" />
<PackageReference Include="Microsoft.Extensions.Diagnostics.HealthChecks" Version="10.0.0" />
```

**Namespace:** `Mcp.DependencyInjection`

**Estimated Size:** 600-1000 lines

---

### 3. Mcp.Http.Core

**Purpose:** HTTP client operations, retry policies, and request/response handling

**Contents:**
- `HttpClientFactory` configuration patterns
- Retry policies with exponential backoff
- Request/response logging middleware
- Common HTTP error handling
- Rate limiting utilities
- Timeout configuration helpers

**Dependencies:**
```xml
<ProjectReference Include="..\Mcp.Common.Core\Mcp.Common.Core.csproj" />
<PackageReference Include="Microsoft.Extensions.Http" Version="10.0.0" />
<PackageReference Include="Microsoft.Extensions.Http.Polly" Version="10.0.0" />
```

**Namespace:** `Mcp.Http`

**Estimated Size:** 800-1200 lines

---

### 4. Mcp.Database.Core

**Purpose:** Shared database connection and operation patterns

**Contents:**
- MongoDB connection management utilities
- Redis connection management utilities
- Common database retry policies
- Database health check patterns
- Connection string parsing/validation
- Connection pooling configuration

**Dependencies:**
```xml
<ProjectReference Include="..\Mcp.Common.Core\Mcp.Common.Core.csproj" />
<PackageReference Include="MongoDB.Driver" Version="3.5.0" />
<PackageReference Include="StackExchange.Redis" Version="2.8.0" />
<PackageReference Include="Microsoft.Data.SqlClient" Version="5.2.0" />
```

**Namespace:** `Mcp.Database`

**Estimated Size:** 1000-1500 lines

---

## Dependency Hierarchy

```
Level 1 (Foundation - No internal dependencies):
└── Mcp.Common.Core

Level 2 (Depends only on Mcp.Common.Core):
├── Mcp.DependencyInjection.Core → Mcp.Common.Core
├── Mcp.Http.Core → Mcp.Common.Core
└── Mcp.Database.Core → Mcp.Common.Core

Existing Shared (Unchanged):
├── SerilogFileWriter (could optionally reference Mcp.Common.Core in future)
├── RegistryTools
└── MsOfficeCrypto
```

**Design Principles:**
- No circular dependencies
- Strict hierarchy prevents complex dependency graphs
- Each library has focused responsibility
- Opt-in usage - servers choose which to reference

---

## Server Library Migration Map

### Phase 1: Mcp.Common.Core

| Server Core Library | What to Extract | Estimated Impact |
|---------------------|-----------------|------------------|
| AwsServer.Core | SerializerOptions.cs | Remove 1 file |
| AzureServer.Core | SerializerOptions.cs, DateTimeExtensions, StringExtensions | Remove 3 files |
| CSharpAnalyzer.Core | SerializerOptions.cs | Remove 1 file |
| DebugServer.Core | SerializerOptions.cs | Remove 1 file |
| DesktopCommander.Core | SerializerOptions.cs | Remove 1 file |
| DocumentServer.Core | SerializerOptions.cs | Remove 1 file |
| MongoServer.Core | SerializerOptions.cs | Remove 1 file |
| SqlServer.Core | SerializerOptions.cs | Remove 1 file |

**Total Files Removed:** 11 files, ~800 lines

---

### Phase 2: Mcp.DependencyInjection.Core

| Server Core Library | What to Extract | Estimated Impact |
|---------------------|-----------------|------------------|
| AwsServer.Core | ServiceCollectionExtensions.cs | Remove 1 file, ~300 lines |
| AzureServer.Core | ServiceCollectionExtensions.cs | Remove 1 file, ~300 lines |
| Others | Common DI patterns | Standardize across all servers |

**Total Files Removed:** 2 files, ~600 lines

---

### Phase 3: Mcp.Http.Core

| Server Core Library | What to Extract | Estimated Impact |
|---------------------|-----------------|------------------|
| AwsServer.Core | HTTP client configuration | Reduce ~200 lines |
| AzureServer.Core | HTTP client configuration | Reduce ~200 lines |
| Playwright.Core | HTTP utilities | Reduce ~200 lines |
| SeleniumChrome.Core | HTTP utilities | Reduce ~200 lines |

**Total Reduction:** ~800 lines

---

### Phase 4: Mcp.Database.Core

| Server Core Library | What to Extract | Estimated Impact |
|---------------------|-----------------|------------------|
| MongoServer.Core | MongoDB connection patterns | Reduce ~300 lines |
| RedisBrowser.Core | Redis connection patterns | Reduce ~300 lines |
| SeleniumChrome.Core | MongoDB connection patterns | Reduce ~200 lines |
| SqlServer.Core | SQL connection patterns | Reduce ~200 lines |

**Total Reduction:** ~1000 lines

---

## Implementation Plan

### Step 1: Create Mcp.Common.Core
- [ ] Create project structure at `Libraries/Mcp.Common.Core/`
- [ ] Extract SerializerOptions from one library as base
- [ ] Add namespace `Mcp.Common`
- [ ] Add DateTimeExtensions from AzureServer.Core
- [ ] Add StringExtensions from AzureServer.Core
- [ ] Create common exception types
- [ ] Create common constants
- [ ] Add unit tests
- [ ] Update all 8 server cores to reference Mcp.Common.Core
- [ ] Remove duplicate files from all servers
- [ ] Update namespaces and imports
- [ ] Test all servers compile and run

### Step 2: Create Mcp.DependencyInjection.Core
- [ ] Create project structure at `Libraries/Mcp.DependencyInjection.Core/`
- [ ] Reference Mcp.Common.Core
- [ ] Extract ServiceCollectionExtensions from AWS
- [ ] Extract ServiceCollectionExtensions from Azure
- [ ] Merge and consolidate patterns
- [ ] Add namespace `Mcp.DependencyInjection`
- [ ] Add unit tests
- [ ] Update AWS and Azure cores to reference
- [ ] Remove duplicate files
- [ ] Test compilation

### Step 3: Create Mcp.Http.Core
- [ ] Create project structure at `Libraries/Mcp.Http.Core/`
- [ ] Reference Mcp.Common.Core
- [ ] Extract HTTP client factory patterns
- [ ] Add retry policies
- [ ] Add logging middleware
- [ ] Add namespace `Mcp.Http`
- [ ] Add unit tests
- [ ] Update 4 server cores to reference
- [ ] Refactor HTTP code to use shared library
- [ ] Test compilation and runtime

### Step 4: Create Mcp.Database.Core
- [ ] Create project structure at `Libraries/Mcp.Database.Core/`
- [ ] Reference Mcp.Common.Core
- [ ] Extract MongoDB connection management
- [ ] Extract Redis connection management
- [ ] Extract SQL connection patterns
- [ ] Add health check utilities
- [ ] Add namespace `Mcp.Database`
- [ ] Add unit tests
- [ ] Update 4 server cores to reference
- [ ] Refactor database code to use shared library
- [ ] Test compilation and runtime

### Step 5: Documentation and Testing
- [ ] Update README.md in each new library
- [ ] Create USAGE_EXAMPLES.md for each library
- [ ] Add XML documentation comments
- [ ] Create integration tests
- [ ] Update main McpServers README
- [ ] Document migration patterns for future servers

---

## Success Metrics

### Code Reduction
- **Target:** Remove 3000+ lines of duplicate code
- **SerializerOptions:** 200 lines removed
- **ServiceCollectionExtensions:** 600 lines removed
- **HTTP patterns:** 800 lines removed
- **Database patterns:** 1000 lines removed
- **Miscellaneous utilities:** 400+ lines removed

### Maintainability
- **Before:** Bug fixes require changes in 8+ locations
- **After:** Bug fixes in 1 location benefit all servers
- **Testing:** Centralized unit tests for common functionality

### Consistency
- All servers use identical patterns for:
  - JSON serialization
  - HTTP client configuration
  - Database connections
  - Dependency injection
  - Error handling

---

## Migration Strategy

### Approach: Gradual, Non-Breaking
1. Create new shared library
2. Add to existing server cores as additional reference
3. Refactor to use shared code (side-by-side with old code)
4. Test thoroughly
5. Remove old duplicate code
6. Repeat for next library

### Risk Mitigation
- Each phase is independent
- Can rollback any phase without affecting others
- Existing functionality remains until replacement is proven
- Comprehensive testing at each step

### Timeline Estimate
- **Phase 1 (Mcp.Common.Core):** 2-3 hours
- **Phase 2 (Mcp.DependencyInjection.Core):** 2-3 hours
- **Phase 3 (Mcp.Http.Core):** 3-4 hours
- **Phase 4 (Mcp.Database.Core):** 3-4 hours
- **Testing & Documentation:** 2-3 hours
- **Total:** 12-17 hours of focused work

---

## Future Considerations

### Additional Shared Libraries (Future Phases)
- **Mcp.Security.Core** - Authentication/authorization patterns
- **Mcp.Caching.Core** - Common caching strategies
- **Mcp.Messaging.Core** - Event/message bus patterns
- **Mcp.Testing.Core** - Common test utilities and mocks

### Version Management
- Use semantic versioning for shared libraries
- Server cores specify minimum compatible version
- Breaking changes increment major version

### CI/CD Integration
- Build shared libraries first in pipeline
- Run shared library tests before server tests
- Cache shared library artifacts

---

## Notes

- All new libraries target .NET 9.0
- Existing libraries (SerilogFileWriter, RegistryTools, MsOfficeCrypto) remain unchanged
- Namespace convention: `Mcp.{Purpose}` for new shared libraries
- Focus on extracting proven patterns, not creating new abstractions
- Document decision rationale in each library's README

---

## Status

**Created:** 2025-11-09
**Last Updated:** 2025-11-09
**Current Phase:** Phase 2 Complete ✅
**Completed:** Mcp.Common.Core + Mcp.DependencyInjection.Core
**Next Step:** Phase 3 - Create Mcp.Http.Core (or continue refactoring Azure services)

### Phase 1 Results
- ✅ Mcp.Common.Core created and builds successfully
- ✅ 8 server cores migrated (AwsServer, Azure, CSharp, Debug, Desktop, Document, Mongo, Sql)
- ✅ 11 duplicate files removed
- ✅ ~800 lines of duplicate code eliminated
- ✅ All builds successful with 0 new errors or warnings
- 📄 See [SHARED_LIBRARIES_PHASE1_COMPLETE.md](./SHARED_LIBRARIES_PHASE1_COMPLETE.md) for details

### Phase 2 Results
- ✅ Mcp.DependencyInjection.Core created with 5 helper extension methods
- ✅ AzureServer.Core/Configuration/Networking.cs refactored (112 lines → 34 lines, 75% reduction)
- ✅ Demonstrated 87.5% reduction in DI registration boilerplate (8 lines → 1 line)
- ✅ All builds successful with 0 new errors or warnings
- ✅ Comprehensive README with before/after examples and migration guide
- 📄 See [SHARED_LIBRARIES_PHASE2_COMPLETE.md](./SHARED_LIBRARIES_PHASE2_COMPLETE.md) for details
