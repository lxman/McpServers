using McpCodeEditor.Services.Advanced;
using McpCodeEditor.Tools.Advanced;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace McpCodeEditor.ServiceModules
{
    public static class AdvancedFileReaderModule
    {
        public static IServiceCollection AddAdvancedFileReaderServices(this IServiceCollection services)
        {
            // Core Advanced File Reading Service
            services.AddScoped<IAdvancedFileReaderService, AdvancedFileReaderService>();
            
            // Advanced File Reader Tools
            services.AddSingleton<AdvancedFileReaderTools>();
            
            // Memory cache for syntax tree caching (if not already registered)
            services.AddMemoryCache();
            
            return services;
        }
    }
}
