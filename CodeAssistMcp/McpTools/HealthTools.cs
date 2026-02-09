using System.ComponentModel;
using System.Text.Json;
using Mcp.Common.Core;
using CodeAssist.Core.Configuration;
using CodeAssist.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace CodeAssistMcp.McpTools;

/// <summary>
/// MCP tools for health checks and diagnostics.
/// </summary>
[McpServerToolType]
public class HealthTools(
    OllamaService ollamaService,
    QdrantService qdrantService,
    IOptions<CodeAssistOptions> options,
    ILogger<HealthTools> logger)
{
    private readonly CodeAssistOptions _options = options.Value;

    [McpServerTool, DisplayName("check_health")]
    [Description("Check if all required services (Ollama, Qdrant) are running and properly configured. Run this first if you encounter errors with indexing or searching.")]
    public async Task<string> CheckHealth()
    {
        (bool isHealthy, bool modelAvailable, string? error) ollamaStatus = await CheckOllamaAsync();
        (bool isHealthy, int collectionsCount, string? error) qdrantStatus = await CheckQdrantAsync();

        bool allHealthy = ollamaStatus.isHealthy && qdrantStatus.isHealthy;

        var result = new
        {
            success = true,
            healthy = allHealthy,
            services = new
            {
                ollama = new
                {
                    url = _options.OllamaUrl,
                    healthy = ollamaStatus.isHealthy,
                    embeddingModel = _options.EmbeddingModel,
                    modelAvailable = ollamaStatus.modelAvailable,
                    error = ollamaStatus.error
                },
                qdrant = new
                {
                    url = _options.QdrantUrl,
                    healthy = qdrantStatus.isHealthy,
                    collectionsCount = qdrantStatus.collectionsCount,
                    error = qdrantStatus.error
                }
            },
            configuration = new
            {
                vectorDimension = _options.VectorDimension,
                maxChunkSize = _options.MaxChunkSize,
                indexStateDirectory = _options.IndexStateDirectory
            },
            recommendations = GetRecommendations(ollamaStatus, qdrantStatus)
        };

        return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
    }

    [McpServerTool, DisplayName("setup_services")]
    [Description("Get instructions for setting up required services (Ollama and Qdrant) if they're not running.")]
    public Task<string> SetupServices()
    {
        var instructions = new
        {
            success = true,
            ollama = new
            {
                description = "Ollama runs local LLMs for generating embeddings",
                install = new
                {
                    macOS = "brew install ollama",
                    linux = "curl -fsSL https://ollama.com/install.sh | sh",
                    windows = "Download from https://ollama.com/download"
                },
                start = "ollama serve",
                pullModel = $"ollama pull {_options.EmbeddingModel}",
                configuredUrl = _options.OllamaUrl
            },
            qdrant = new
            {
                description = "Qdrant is the vector database for storing embeddings",
                docker = "docker run -d -p 6333:6333 -p 6334:6334 -v qdrant_storage:/qdrant/storage qdrant/qdrant",
                dockerCompose = @"
services:
  qdrant:
    image: qdrant/qdrant
    ports:
      - ""6333:6333""
      - ""6334:6334""
    volumes:
      - qdrant_storage:/qdrant/storage
volumes:
  qdrant_storage:
",
                configuredUrl = _options.QdrantUrl
            }
        };

        return Task.FromResult(JsonSerializer.Serialize(instructions, SerializerOptions.JsonOptionsIndented));
    }

    [McpServerTool, DisplayName("pull_embedding_model")]
    [Description("Download the configured embedding model to Ollama. Required before indexing if the model isn't already available.")]
    public async Task<string> PullEmbeddingModel()
    {
        try
        {
            logger.LogInformation("Pulling embedding model {Model}", _options.EmbeddingModel);

            await ollamaService.EnsureModelAvailableAsync();

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = $"Model '{_options.EmbeddingModel}' is ready",
                model = _options.EmbeddingModel
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to pull embedding model");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                hint = "Make sure Ollama is running: ollama serve"
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    private async Task<(bool isHealthy, bool modelAvailable, string? error)> CheckOllamaAsync()
    {
        try
        {
            bool modelAvailable = await ollamaService.IsModelAvailableAsync();
            return (true, modelAvailable, null);
        }
        catch (HttpRequestException ex)
        {
            return (false, false, $"Cannot connect to Ollama at {_options.OllamaUrl}: {ex.Message}");
        }
        catch (Exception ex)
        {
            return (false, false, ex.Message);
        }
    }

    private async Task<(bool isHealthy, int collectionsCount, string? error)> CheckQdrantAsync()
    {
        try
        {
            List<string> collections = await qdrantService.ListCollectionsAsync();
            return (true, collections.Count, null);
        }
        catch (Exception ex) when (ex.Message.Contains("refused") || ex.Message.Contains("connect"))
        {
            return (false, 0, $"Cannot connect to Qdrant at {_options.QdrantUrl}: {ex.Message}");
        }
        catch (Exception ex)
        {
            return (false, 0, ex.Message);
        }
    }

    private List<string> GetRecommendations(
        (bool isHealthy, bool modelAvailable, string? error) ollama,
        (bool isHealthy, int collectionsCount, string? error) qdrant)
    {
        var recommendations = new List<string>();

        if (!ollama.isHealthy)
        {
            recommendations.Add($"Start Ollama: ollama serve");
        }
        else if (!ollama.modelAvailable)
        {
            recommendations.Add($"Pull the embedding model: ollama pull {_options.EmbeddingModel}");
        }

        if (!qdrant.isHealthy)
        {
            recommendations.Add("Start Qdrant: docker run -p 6333:6333 qdrant/qdrant");
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add("All services healthy - ready to index repositories!");
        }

        return recommendations;
    }
}
