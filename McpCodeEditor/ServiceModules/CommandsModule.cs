using McpCodeEditor.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace McpCodeEditor.ServiceModules;

/// <summary>
/// Service module for registering refactoring commands.
/// Part of Phase 3 Task 2 implementation - Command Pattern for refactoring operations.
/// </summary>
public static class CommandsModule
{
    /// <summary>
    /// Registers all refactoring commands as singleton services.
    /// Commands implement the Command Pattern to encapsulate refactoring operations.
    /// </summary>
    /// <param name="services">Service collection to configure</param>
    /// <returns>Configured service collection</returns>
    public static IServiceCollection AddCommands(this IServiceCollection services)
    {
        // Register individual command implementations
        // FIXED: Changed from Scoped to Singleton for consistency with the orchestrator
        services.AddSingleton<ExtractMethodCommand>();
        services.AddSingleton<InlineMethodCommand>();
        services.AddSingleton<IntroduceVariableCommand>();
        services.AddSingleton<OrganizeImportsCommand>();
        services.AddSingleton<RenameSymbolCommand>();

        // Register command factory for dynamic command resolution
        services.AddSingleton<ICommandFactory, CommandFactory>();

        return services;
    }
}

/// <summary>
/// Factory interface for creating refactoring commands by ID.
/// </summary>
public interface ICommandFactory
{
    /// <summary>
    /// Creates a command instance by command ID.
    /// </summary>
    /// <param name="commandId">The command identifier</param>
    /// <returns>The command instance, or null if not found</returns>
    IRefactoringCommand? CreateCommand(string commandId);
    
    /// <summary>
    /// Gets all available commands.
    /// </summary>
    /// <returns>Collection of all registered commands</returns>
    IEnumerable<IRefactoringCommand> GetAllCommands();
}

/// <summary>
/// Factory implementation for creating refactoring commands.
/// </summary>
public class CommandFactory : ICommandFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, Type> _commandTypes;

    public CommandFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        
        // Map command IDs to their types
        _commandTypes = new Dictionary<string, Type>
        {
            ["extract-method"] = typeof(ExtractMethodCommand),
            ["inline-method"] = typeof(InlineMethodCommand),
            ["introduce-variable"] = typeof(IntroduceVariableCommand),
            ["organize-imports"] = typeof(OrganizeImportsCommand),
            ["rename-symbol"] = typeof(RenameSymbolCommand)
        };
    }

    public IRefactoringCommand? CreateCommand(string commandId)
    {
        if (string.IsNullOrEmpty(commandId) || !_commandTypes.TryGetValue(commandId, out Type? commandType))
        {
            return null;
        }

        return (IRefactoringCommand?)_serviceProvider.GetService(commandType);
    }

    public IEnumerable<IRefactoringCommand> GetAllCommands()
    {
        return _commandTypes.Values
            .Select(type => (IRefactoringCommand?)_serviceProvider.GetService(type))
            .Where(command => command != null)
            .Cast<IRefactoringCommand>();
    }
}