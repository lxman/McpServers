using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mcp.DependencyInjection.Core;

/// <summary>
/// Extension methods for IServiceCollection that reduce boilerplate in dependency injection registration
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a scoped service with automatic logger resolution from provider or fallback factory.
    /// Reduces boilerplate for the common pattern:
    /// services.AddScoped&lt;IService&gt;(provider => {
    ///     var logger = provider.GetService&lt;ILogger&lt;Service&gt;&gt;() ?? loggerFactory.CreateLogger&lt;Service&gt;();
    ///     var factory = provider.GetRequiredService&lt;Factory&gt;();
    ///     return new Service(factory, logger);
    /// });
    /// </summary>
    /// <typeparam name="TInterface">The service interface type</typeparam>
    /// <typeparam name="TImplementation">The service implementation type</typeparam>
    /// <typeparam name="TFactory">The factory type required by the service</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="loggerFactory">Fallback logger factory if provider doesn't have a logger</param>
    /// <returns>The service collection for chaining</returns>
    /// <example>
    /// services.AddScopedWithFactory&lt;IMyService, MyService, MyFactory&gt;(loggerFactory);
    /// </example>
    public static IServiceCollection AddScopedWithFactory<TInterface, TImplementation, TFactory>(
        this IServiceCollection services,
        ILoggerFactory loggerFactory)
        where TImplementation : class, TInterface
        where TInterface : class
        where TFactory : class
    {
        services.AddScoped<TInterface>(provider =>
        {
            // Get logger from provider or use fallback factory
            ILogger<TImplementation>? logger = provider.GetService<ILogger<TImplementation>>() ??
                                               loggerFactory.CreateLogger<TImplementation>();

            // Get required factory
            TFactory factory = provider.GetRequiredService<TFactory>();

            // Create instance via Activator
            return (TInterface)Activator.CreateInstance(typeof(TImplementation), factory, logger)!;
        });

        return services;
    }

    /// <summary>
    /// Registers a scoped service with automatic logger resolution and no additional dependencies.
    /// Use this when the service only requires a logger.
    /// </summary>
    /// <typeparam name="TInterface">The service interface type</typeparam>
    /// <typeparam name="TImplementation">The service implementation type</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="loggerFactory">Fallback logger factory if provider doesn't have a logger</param>
    /// <returns>The service collection for chaining</returns>
    /// <example>
    /// services.AddScopedWithLogger&lt;IMyService, MyService&gt;(loggerFactory);
    /// </example>
    public static IServiceCollection AddScopedWithLogger<TInterface, TImplementation>(
        this IServiceCollection services,
        ILoggerFactory loggerFactory)
        where TImplementation : class, TInterface
        where TInterface : class
    {
        services.AddScoped<TInterface>(provider =>
        {
            ILogger<TImplementation>? logger = provider.GetService<ILogger<TImplementation>>() ??
                                               loggerFactory.CreateLogger<TImplementation>();

            return (TInterface)Activator.CreateInstance(typeof(TImplementation), logger)!;
        });

        return services;
    }

    /// <summary>
    /// Registers a singleton service with automatic logger resolution from provider or fallback factory.
    /// </summary>
    /// <typeparam name="TInterface">The service interface type</typeparam>
    /// <typeparam name="TImplementation">The service implementation type</typeparam>
    /// <typeparam name="TFactory">The factory type required by the service</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="loggerFactory">Fallback logger factory if provider doesn't have a logger</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddSingletonWithFactory<TInterface, TImplementation, TFactory>(
        this IServiceCollection services,
        ILoggerFactory loggerFactory)
        where TImplementation : class, TInterface
        where TInterface : class
        where TFactory : class
    {
        services.AddSingleton<TInterface>(provider =>
        {
            ILogger<TImplementation>? logger = provider.GetService<ILogger<TImplementation>>() ??
                                               loggerFactory.CreateLogger<TImplementation>();

            TFactory factory = provider.GetRequiredService<TFactory>();

            return (TInterface)Activator.CreateInstance(typeof(TImplementation), factory, logger)!;
        });

        return services;
    }

    /// <summary>
    /// Registers a singleton service with automatic logger resolution and no additional dependencies.
    /// </summary>
    /// <typeparam name="TInterface">The service interface type</typeparam>
    /// <typeparam name="TImplementation">The service implementation type</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="loggerFactory">Fallback logger factory if provider doesn't have a logger</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddSingletonWithLogger<TInterface, TImplementation>(
        this IServiceCollection services,
        ILoggerFactory loggerFactory)
        where TImplementation : class, TInterface
        where TInterface : class
    {
        services.AddSingleton<TInterface>(provider =>
        {
            ILogger<TImplementation>? logger = provider.GetService<ILogger<TImplementation>>() ??
                                               loggerFactory.CreateLogger<TImplementation>();

            return (TInterface)Activator.CreateInstance(typeof(TImplementation), logger)!;
        });

        return services;
    }

    /// <summary>
    /// Registers a scoped service with automatic logger resolution and custom factory function.
    /// Use when you need more control over service instantiation.
    /// </summary>
    /// <typeparam name="TInterface">The service interface type</typeparam>
    /// <typeparam name="TImplementation">The service implementation type</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="loggerFactory">Fallback logger factory if provider doesn't have a logger</param>
    /// <param name="factory">Custom factory function that receives logger and service provider</param>
    /// <returns>The service collection for chaining</returns>
    /// <example>
    /// services.AddScopedWithLoggerAndFactory&lt;IMyService, MyService&gt;(
    ///     loggerFactory,
    ///     (logger, provider) => new MyService(
    ///         provider.GetRequiredService&lt;Dependency1&gt;(),
    ///         provider.GetRequiredService&lt;Dependency2&gt;(),
    ///         logger
    ///     )
    /// );
    /// </example>
    public static IServiceCollection AddScopedWithLoggerAndFactory<TInterface, TImplementation>(
        this IServiceCollection services,
        ILoggerFactory loggerFactory,
        Func<ILogger<TImplementation>, IServiceProvider, TImplementation> factory)
        where TImplementation : class, TInterface
        where TInterface : class
    {
        services.AddScoped<TInterface>(provider =>
        {
            ILogger<TImplementation>? logger = provider.GetService<ILogger<TImplementation>>() ??
                                               loggerFactory.CreateLogger<TImplementation>();

            return factory(logger, provider);
        });

        return services;
    }
}
