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

        // modelAvailable belongs in this AND: it is a functional probe (can we actually embed?), while
        // isHealthy only says the service answered. Leaving it out let check_health report
        // "healthy: true" for a whole session in which every single search failed with a 500, because
        // the embedding server was up and answering /api/tags while its embed path threw. A health
        // flag that is true when nothing works is worse than no flag — it is the one signal a caller
        // checks before concluding the problem lies somewhere else.
        bool allHealthy = ollamaStatus.isHealthy && ollamaStatus.modelAvailable && qdrantStatus.isHealthy;

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
        catch (Exception ex) when (ex.Message.Contains("refused") || ex.Message.Contains("connect") ||
                                   ex.Message.Contains("Unavailable") || ex.Message.Contains("Retry"))
        {
            // Reset the connection so the next operation creates a fresh gRPC client
            qdrantService.ResetConnection();
            logger.LogWarning("Qdrant health check failed — connection reset for next attempt");
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

        // Advice has to match the server actually configured. Telling someone to "ollama pull" against
        // the MLX server sends them after a fix that cannot exist: it loads one model at startup and
        // has no pull endpoint at all.
        if (!ollama.isHealthy)
        {
            recommendations.Add(_options.IsOllamaServer
                ? "Start Ollama: ollama serve"
                : $"Start the MLX embedding server (expected at {_options.OllamaUrl}): "
                  + "cd CodeAssistMcp/mlx-server && .venv-embed/bin/python server.py --port 11435");
        }
        else if (!ollama.modelAvailable)
        {
            recommendations.Add(_options.IsOllamaServer
                ? $"Pull the embedding model: ollama pull {_options.EmbeddingModel}"
                : $"The MLX server at {_options.OllamaUrl} is reachable but cannot embed. It serves one "
                  + $"model and cannot pull '{_options.EmbeddingModel}' — check that the configured name "
                  + "matches what it loaded (GET /api/tags), and check the server log for a model-load "
                  + "or tokenizer error.");
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
