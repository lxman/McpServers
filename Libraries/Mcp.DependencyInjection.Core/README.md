# Mcp.DependencyInjection.Core

Common dependency injection patterns and helper extension methods for MCP servers to reduce boilerplate code.

## Purpose

This library provides reusable dependency injection patterns that eliminate repetitive service registration code. It's particularly useful for scenarios where services require logger injection with fallback factories, which is a common pattern across MCP servers.

## Installation

Add a project reference in your server's `.csproj` file:

```xml
<ItemGroup>
  <ProjectReference Include="..\Mcp.DependencyInjection.Core\Mcp.DependencyInjection.Core.csproj" />
</ItemGroup>
```

## API Reference

### AddScopedWithFactory

Registers a scoped service with automatic logger resolution and a required factory dependency.

**Signature:**
```csharp
public static IServiceCollection AddScopedWithFactory<TInterface, TImplementation, TFactory>(
    this IServiceCollection services,
    ILoggerFactory loggerFactory)
```

**Before (Repetitive Boilerplate):**
```csharp
services.AddScoped<IApplicationGatewayService>(provider =>
{
    ILogger<ApplicationGatewayService> logger = provider.GetService<ILogger<ApplicationGatewayService>>() ??
                                                loggerFactory.CreateLogger<ApplicationGatewayService>();
    var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
    return new ApplicationGatewayService(armClientFactory, logger);
});

services.AddScoped<IExpressRouteService>(provider =>
{
    ILogger<ExpressRouteService> logger = provider.GetService<ILogger<ExpressRouteService>>() ??
                                          loggerFactory.CreateLogger<ExpressRouteService>();
    var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
    return new ExpressRouteService(armClientFactory, logger);
});

// ...repeated 10 more times
```

**After (Clean and Concise):**
```csharp
using Mcp.DependencyInjection;

services.AddScopedWithFactory<IApplicationGatewayService, ApplicationGatewayService, ArmClientFactory>(loggerFactory);
services.AddScopedWithFactory<IExpressRouteService, ExpressRouteService, ArmClientFactory>(loggerFactory);
// ...continues for all services
```

**Reduction:** From ~8 lines per registration to 1 line (87.5% reduction)

---

### AddScopedWithLogger

Registers a scoped service that only requires a logger (no other dependencies).

**Signature:**
```csharp
public static IServiceCollection AddScopedWithLogger<TInterface, TImplementation>(
    this IServiceCollection services,
    ILoggerFactory loggerFactory)
```

**Usage:**
```csharp
services.AddScopedWithLogger<IMyService, MyService>(loggerFactory);
```

**Equivalent To:**
```csharp
services.AddScoped<IMyService>(provider =>
{
    ILogger<MyService> logger = provider.GetService<ILogger<MyService>>() ??
                                loggerFactory.CreateLogger<MyService>();
    return new MyService(logger);
});
```

---

### AddSingletonWithFactory

Registers a singleton service with automatic logger resolution and a required factory dependency.

**Signature:**
```csharp
public static IServiceCollection AddSingletonWithFactory<TInterface, TImplementation, TFactory>(
    this IServiceCollection services,
    ILoggerFactory loggerFactory)
```

**Usage:**
```csharp
services.AddSingletonWithFactory<ICredentialService, CredentialService, CredentialFactory>(loggerFactory);
```

---

### AddSingletonWithLogger

Registers a singleton service that only requires a logger.

**Signature:**
```csharp
public static IServiceCollection AddSingletonWithLogger<TInterface, TImplementation>(
    this IServiceCollection services,
    ILoggerFactory loggerFactory)
```

**Usage:**
```csharp
services.AddSingletonWithLogger<ICacheService, CacheService>(loggerFactory);
```

---

### AddScopedWithLoggerAndFactory

Registers a scoped service with custom factory function for complex instantiation scenarios.

**Signature:**
```csharp
public static IServiceCollection AddScopedWithLoggerAndFactory<TInterface, TImplementation>(
    this IServiceCollection services,
    ILoggerFactory loggerFactory,
    Func<ILogger<TImplementation>, IServiceProvider, TImplementation> factory)
```

**Usage (Multiple Dependencies):**
```csharp
services.AddScopedWithLoggerAndFactory<IComplexService, ComplexService>(
    loggerFactory,
    (logger, provider) => new ComplexService(
        provider.GetRequiredService<Dependency1>(),
        provider.GetRequiredService<Dependency2>(),
        provider.GetRequiredService<Dependency3>(),
        logger
    )
);
```

## Real-World Example: Azure Networking Services

**Before Mcp.DependencyInjection.Core:**

```csharp
// Networking.cs - 112 lines for 12 services
public static IServiceCollection AddNetworkingServices(this IServiceCollection services, ILoggerFactory loggerFactory)
{
    services.AddScoped<IApplicationGatewayService>(provider =>
    {
        ILogger<ApplicationGatewayService> logger = provider.GetService<ILogger<ApplicationGatewayService>>() ??
                                                    loggerFactory.CreateLogger<ApplicationGatewayService>();
        var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
        return new ApplicationGatewayService(armClientFactory, logger);
    });

    services.AddScoped<IExpressRouteService>(provider =>
    {
        ILogger<ExpressRouteService> logger = provider.GetService<ILogger<ExpressRouteService>>() ??
                                              loggerFactory.CreateLogger<ExpressRouteService>();
        var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
        return new ExpressRouteService(armClientFactory, logger);
    });

    // ... 10 more identical patterns (84 more lines)

    return services;
}
```

**After Mcp.DependencyInjection.Core:**

```csharp
// Networking.cs - 28 lines for 12 services
using Mcp.DependencyInjection;

public static IServiceCollection AddNetworkingServices(this IServiceCollection services, ILoggerFactory loggerFactory)
{
    services.AddScopedWithFactory<IApplicationGatewayService, ApplicationGatewayService, ArmClientFactory>(loggerFactory);
    services.AddScopedWithFactory<IExpressRouteService, ExpressRouteService, ArmClientFactory>(loggerFactory);
    services.AddScopedWithFactory<ILoadBalancerService, LoadBalancerService, ArmClientFactory>(loggerFactory);
    services.AddScopedWithFactory<INetworkInterfaceService, NetworkInterfaceService, ArmClientFactory>(loggerFactory);
    services.AddScopedWithFactory<IPrivateEndpointService, PrivateEndpointService, ArmClientFactory>(loggerFactory);
    services.AddScopedWithFactory<IPublicIpAddressService, PublicIpAddressService, ArmClientFactory>(loggerFactory);
    services.AddScopedWithFactory<ISecurityRuleService, SecurityRuleService, ArmClientFactory>(loggerFactory);
    services.AddScopedWithFactory<ISubnetService, SubnetService, ArmClientFactory>(loggerFactory);
    services.AddScopedWithFactory<IVirtualNetworkService, VirtualNetworkService, ArmClientFactory>(loggerFactory);
    services.AddScopedWithFactory<IVpnGatewayService, VpnGatewayService, ArmClientFactory>(loggerFactory);
    services.AddScopedWithFactory<INetworkSecurityGroupService, NetworkSecurityGroupService, ArmClientFactory>(loggerFactory);
    services.AddScopedWithFactory<INetworkWatcherService, NetworkWatcherService, ArmClientFactory>(loggerFactory);

    return services;
}
```

**Reduction:** 112 lines → 28 lines (75% reduction, 84 lines eliminated)

## Requirements

- **Constructor Pattern:** Services must accept factory and logger as constructor parameters
  ```csharp
  public MyService(MyFactory factory, ILogger<MyService> logger)
  {
      _factory = factory;
      _logger = logger;
  }
  ```

- **Service Lifetime:** The factory type (`TFactory`) must already be registered in the service collection

## Benefits

### 1. Code Reduction
- **75-87% less code** for service registration
- Eliminates repetitive boilerplate across hundreds of lines

### 2. Consistency
- Standardized pattern for logger injection with fallbacks
- Uniform service registration approach across all MCP servers

### 3. Maintainability
- Changes to registration pattern occur in one place
- Easier to spot and fix registration issues
- Reduced cognitive load when reading service configuration

### 4. Type Safety
- Generic constraints ensure proper type relationships
- Compile-time verification of service contracts

## Migration Guide

### Step 1: Add Project Reference

```xml
<ProjectReference Include="..\Mcp.DependencyInjection.Core\Mcp.DependencyInjection.Core.csproj" />
```

### Step 2: Add Using Statement

```csharp
using Mcp.DependencyInjection;
```

### Step 3: Replace Verbose Registrations

**Find this pattern:**
```csharp
services.AddScoped<IMyService>(provider =>
{
    ILogger<MyService> logger = provider.GetService<ILogger<MyService>>() ??
                                loggerFactory.CreateLogger<MyService>();
    var factory = provider.GetRequiredService<MyFactory>();
    return new MyService(factory, logger);
});
```

**Replace with:**
```csharp
services.AddScopedWithFactory<IMyService, MyService, MyFactory>(loggerFactory);
```

### Step 4: Build and Test

```bash
dotnet build
# Verify all services still resolve correctly
```

## Advanced Scenarios

### Conditional Registration

```csharp
if (config.FeatureEnabled)
{
    services.AddScopedWithFactory<IFeature, Feature, FeatureFactory>(loggerFactory);
}
```

### Multiple Factories

```csharp
// When different services need different factories
services.AddScopedWithFactory<IServiceA, ServiceA, FactoryA>(loggerFactory);
services.AddScopedWithFactory<IServiceB, ServiceB, FactoryB>(loggerFactory);
```

### Chain Registration

```csharp
services
    .AddScopedWithFactory<IService1, Service1, Factory>(loggerFactory)
    .AddScopedWithFactory<IService2, Service2, Factory>(loggerFactory)
    .AddScopedWithFactory<IService3, Service3, Factory>(loggerFactory);
```

## Performance

- **No runtime overhead:** Helpers compile to identical IL as manual registration
- **Reflection used only during registration:** `Activator.CreateInstance` at startup, not per-request
- **Same performance as manual DI:** No proxies, no dynamic code generation

## Dependencies

- **Mcp.Common.Core** - Foundation library for shared MCP utilities
- **Microsoft.Extensions.DependencyInjection.Abstractions** (10.0.0-rc.2.25502.107)
- **Microsoft.Extensions.Logging.Abstractions** (10.0.0-rc.2.25502.107)

## Target Framework

- .NET 9.0

## Documentation

All public methods include comprehensive XML documentation visible in IntelliSense.

## Version History

### 1.0.0 (2025-11-09)
- Initial release
- Added `AddScopedWithFactory`
- Added `AddScopedWithLogger`
- Added `AddSingletonWithFactory`
- Added `AddSingletonWithLogger`
- Added `AddScopedWithLoggerAndFactory`

## Contributing

When adding new helper methods:
1. Follow existing naming conventions (`Add[Lifetime]With[Pattern]`)
2. Include comprehensive XML documentation
3. Provide before/after examples in README
4. Maintain backward compatibility

## License

Part of the McpServers library collection.
