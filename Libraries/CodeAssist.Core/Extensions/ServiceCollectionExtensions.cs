using CodeAssist.Core.Analysis;
using CodeAssist.Core.Analysis.Roslyn;
using CodeAssist.Core.Caching;
using CodeAssist.Core.Chunking;
using CodeAssist.Core.Configuration;
using CodeAssist.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CodeAssist.Core.Extensions;

/// <summary>
/// Extension methods for registering CodeAssist services.
/// </summary>
public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Add CodeAssist services to the service collection.
        /// </summary>
        public IServiceCollection AddCodeAssistServices(IConfiguration? configuration = null)
        {
            // Register configuration
            if (configuration != null)
            {
                services.Configure<CodeAssistOptions>(
                    configuration.GetSection(CodeAssistOptions.SectionName));
            }
            else
            {
                services.Configure<CodeAssistOptions>(_ => { });
            }

            // Register chunkers - TreeSitterChunker for AST-based chunking, DefaultChunker as fallback
            services.AddSingleton<TreeSitterChunker>();
            services.AddSingleton<DefaultChunker>();
            services.AddSingleton<ChunkerFactory>();

            // Register core services
            services.AddSingleton<OllamaService>();
            services.AddSingleton<QdrantService>();
            services.AddSingleton<RepositoryIndexer>();
            services.AddSingleton<DataFlowGraphService>();

            // Register L1/L2 caching services
            services.AddSingleton<HotCache>();
            services.AddSingleton<FileWatcherService>();
            services.AddSingleton<L2PromotionService>();
            services.AddSingleton<UnifiedSearchService>();

            // Register semantic analysis (Tier 2)
            services.AddSingleton<RoslynSemanticAnalyzer>();
            services.AddSingleton<SemanticAnalyzerRegistry>(sp =>
            {
                var registry = new SemanticAnalyzerRegistry();
                registry.Register(sp.GetRequiredService<RoslynSemanticAnalyzer>());
                return registry;
            });

            return services;
        }

        /// <summary>
        /// Add CodeAssist services with custom configuration.
        /// </summary>
        public IServiceCollection AddCodeAssistServices(Action<CodeAssistOptions> configureOptions)
        {
            services.Configure(configureOptions);

            // Register chunkers - TreeSitterChunker for AST-based chunking, DefaultChunker as fallback
            services.AddSingleton<TreeSitterChunker>();
            services.AddSingleton<DefaultChunker>();
            services.AddSingleton<ChunkerFactory>();

            // Register core services
            services.AddSingleton<OllamaService>();
            services.AddSingleton<QdrantService>();
            services.AddSingleton<RepositoryIndexer>();
            services.AddSingleton<DataFlowGraphService>();

            // Register L1/L2 caching services
            services.AddSingleton<HotCache>();
            services.AddSingleton<FileWatcherService>();
            services.AddSingleton<L2PromotionService>();
            services.AddSingleton<UnifiedSearchService>();

            // Register semantic analysis (Tier 2)
            services.AddSingleton<RoslynSemanticAnalyzer>();
            services.AddSingleton<SemanticAnalyzerRegistry>(sp =>
            {
                var registry = new SemanticAnalyzerRegistry();
                registry.Register(sp.GetRequiredService<RoslynSemanticAnalyzer>());
                return registry;
            });

            return services;
        }
    }
}
