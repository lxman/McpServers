# Shared Libraries Phase 5: Enhanced Connection Management

**Phase:** 5 - Implement Redis and SQL Connection Management
**Status:** 📋 Planning
**Date:** January 2025

## Objectives

1. Refactor RedisBrowser.Core to use Mcp.Database.Core.Redis.RedisConnectionManager
2. Refactor SqlServer.Core to use Mcp.Database.Core.Sql.SqlConnectionManager
3. Enable AzureServer.Core to use shared SQL connection management for Azure SQL operations

## Current State Analysis

### What's Available in Mcp.Database.Core

#### Redis (Mcp.Database.Core.Redis)
- ✅ `RedisConnectionManager` - Full connection management with health monitoring
- ✅ `RedisConnectionOptions` - Configuration options
- ✅ `RedisServiceCollectionExtensions` - DI integration
- **Features:**
  - Multiple connection support
  - Database selection (0-15)
  - Health monitoring with auto-cleanup
  - Connection pooling
  - Ping/connectivity tests

#### SQL (Mcp.Database.Core.Sql)
- ✅ `SqlConnectionManager` - Multi-provider connection management
- ✅ `SqlConnectionOptions` - Configuration options
- ✅ `ISqlProvider` interface
- ✅ Three provider implementations:
  - `SqlServerProvider` - Microsoft SQL Server
  - `PostgreSqlProvider` - PostgreSQL
  - `MySqlProvider` - MySQL
- **Features:**
  - Multi-provider support (SQL Server, PostgreSQL, MySQL)
  - Health monitoring with auto-cleanup
  - Connection pooling
  - Provider-specific optimizations

### Current Implementation in Target Libraries

#### RedisBrowser.Core
**Current State:**
- Custom connection management in `RedisService.cs`
- Single connection at a time
- Manual connection string handling
- Basic connection lifecycle management

**What Needs to Change:**
- Replace custom connection logic with `RedisConnectionManager`
- Update `RedisService` to use shared manager
- Add multi-connection support
- Enable health monitoring

**Estimated Impact:**
- Lines to eliminate: ~100-150 (connection management code)
- Files to modify: 1 (RedisService.cs)
- Files to delete: 0 (already clean after Phase 4A)

#### SqlServer.Core
**Current State:**
- Has basic `ConnectionManager` for SQL Server only
- Limited to SQL Server provider
- No health monitoring
- Basic connection pooling

**What Needs to Change:**
- Replace `ConnectionManager` with `SqlConnectionManager`
- Enable multi-provider support (SQL Server, PostgreSQL, MySQL)
- Add health monitoring capabilities
- Update all service classes to use shared manager

**Estimated Impact:**
- Lines to eliminate: ~80-100 (custom ConnectionManager)
- Files to modify: Multiple (all services using ConnectionManager)
- Files to delete: 1 (ConnectionManager.cs)

#### AzureServer.Core
**Current State:**
- Has references to Mcp.Database.Core (added in Phase 4A)
- Currently uses Azure SDK connection methods
- No centralized SQL connection management

**What Needs to Change:**
- Add `SqlConnectionManager` usage for Azure SQL Database operations
- Integrate with existing Azure services
- Enable connection pooling for Azure SQL

**Estimated Impact:**
- Lines to add: Configuration and integration code
- Files to modify: Azure SQL service classes
- Improvement: Better connection management for Azure SQL

## Detailed Plan

### Task 1: Refactor RedisBrowser.Core

#### 1.1 Update RedisService.cs
**Changes:**
- Add `RedisConnectionManager` dependency injection
- Replace custom connection logic
- Update methods to use connection manager
- Add multi-connection support

**API Mapping:**
```csharp
// OLD (custom logic)
private ConnectionMultiplexer? _connection;
await ConnectionMultiplexer.ConnectAsync(connectionString);

// NEW (shared manager)
private readonly RedisConnectionManager _connectionManager;
await _connectionManager.AddConnectionAsync(connectionName, connectionString, database);
```

#### 1.2 Update RedisMcp DI Registration
**Changes:**
- Register `RedisConnectionManager` as singleton
- Update `RedisService` constructor to inject manager
- Configure health monitoring options

### Task 2: Refactor SqlServer.Core

#### 2.1 Replace ConnectionManager
**Files to Update:**
- `Services/SqlServerService.cs` (or equivalent)
- All service classes using ConnectionManager

**Changes:**
- Replace custom `ConnectionManager` with `SqlConnectionManager`
- Update to support multiple providers (SQL Server primary, PostgreSQL/MySQL secondary)
- Enable health monitoring

#### 2.2 Update SqlMcp DI Registration
**Changes:**
- Register `SqlConnectionManager` as singleton
- Configure provider options
- Update service registrations

### Task 3: Enhance AzureServer.Core

#### 3.1 Add SQL Connection Management
**Services to Update:**
- Azure SQL Database services
- Any service executing SQL queries

**Changes:**
- Inject `SqlConnectionManager` where needed
- Use connection pooling for Azure SQL operations
- Enable health monitoring for Azure SQL connections

#### 3.2 Update AzureMcp DI Registration
**Changes:**
- Optionally register `SqlConnectionManager` if Azure SQL services need it
- Configure for Azure SQL provider

## Expected Benefits

### Code Reduction
- **RedisBrowser.Core:** ~100-150 lines eliminated
- **SqlServer.Core:** ~80-100 lines eliminated
- **Total:** ~180-250 lines eliminated

### Feature Additions
- Multi-connection support in RedisBrowser
- Multi-provider support in SqlServer (PostgreSQL, MySQL in addition to SQL Server)
- Health monitoring across all database connections
- Consistent connection management API across MongoDB, Redis, and SQL
- Auto-cleanup of unhealthy connections

### Architecture Improvements
- Consistent connection patterns across all database types
- Centralized health monitoring
- Better resource management with connection pooling
- Easier to add new database types in future

## Success Criteria

1. ✅ All projects build with 0 errors
2. ✅ RedisBrowser.Core uses `RedisConnectionManager`
3. ✅ SqlServer.Core uses `SqlConnectionManager`
4. ✅ AzureServer.Core can optionally use `SqlConnectionManager` for Azure SQL
5. ✅ All MCP servers pass integration tests
6. ✅ Health monitoring works across all connection types
7. ✅ ~180-250 lines of duplicate code eliminated

## Risk Assessment

**Low Risk Areas:**
- RedisBrowser.Core - Small, focused refactoring
- Build verification - Already have good build infrastructure

**Medium Risk Areas:**
- SqlServer.Core - Multiple services may depend on ConnectionManager
- DI configuration - Need to ensure proper registration

**Mitigation:**
- Incremental refactoring (one library at a time)
- Build after each major change
- Comprehensive testing of connection lifecycle

## Timeline Estimate

1. **RedisBrowser.Core refactoring:** 30-45 minutes
2. **SqlServer.Core refactoring:** 45-60 minutes
3. **AzureServer.Core enhancement:** 30-45 minutes
4. **Testing and verification:** 15-30 minutes
5. **Documentation:** 30 minutes

**Total Estimated Time:** 2.5-4 hours

## Next Steps After Phase 5

1. **Phase 6:** Cross-database operations (e.g., MongoDB → SQL data sync)
2. **Phase 7:** Unified health monitoring dashboard across all databases
3. **Phase 8:** Connection pooling optimizations and metrics

## Questions to Address

1. Should RedisBrowser support multiple Redis connections simultaneously?
   - **Recommendation:** Yes, consistent with MongoDB approach

2. Should SqlServer.Core support PostgreSQL and MySQL in addition to SQL Server?
   - **Recommendation:** Yes, shared manager supports all three

3. Should AzureServer.Core use SqlConnectionManager for all Azure SQL operations?
   - **Recommendation:** Optional - use where it makes sense, Azure SDK where appropriate

4. Should we maintain public API compatibility?
   - **Recommendation:** Yes, same as Phase 4A (update internals, keep public APIs stable)

## Approval Required

Before proceeding with Phase 5 implementation, confirm:
- ✅ Approach is acceptable
- ✅ All target libraries identified
- ✅ Success criteria are clear
- ✅ Timeline is reasonable

---

**Status:** Awaiting approval to proceed with implementation
