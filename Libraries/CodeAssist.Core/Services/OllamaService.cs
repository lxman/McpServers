using CodeAssist.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OllamaSharp;
using OllamaSharp.Models;

namespace CodeAssist.Core.Services;

/// <summary>
/// Service for generating embeddings using Ollama.
/// </summary>
public sealed class OllamaService
{
    private readonly OllamaApiClient _client;
    private readonly CodeAssistOptions _options;
    private readonly ILogger<OllamaService> _logger;

    public OllamaService(
        IOptions<CodeAssistOptions> options,
        ILogger<OllamaService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _client = new OllamaApiClient(_options.OllamaUrl);
    }

    /// <summary>
    /// Generate embeddings for a single text.
    /// </summary>
    public async Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            EmbedResponse response = await _client.EmbedAsync(new EmbedRequest
            {
                Model = _options.EmbeddingModel,
                Input = [text]
            }, cancellationToken);

            if (response.Embeddings.Count == 0)
            {
                throw new InvalidOperationException("Ollama returned no embeddings");
            }

            return response.Embeddings[0];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embedding for text of length {Length}", text.Length);
            throw;
        }
    }

    /// <summary>
    /// Generate embeddings for multiple texts in batch.
    /// </summary>
    public async Task<float[][]> GetEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        if (texts.Count == 0)
        {
            return [];
        }

        try
        {
            _logger.LogDebug("Generating embeddings for {Count} texts", texts.Count);

            EmbedResponse response = await _client.EmbedAsync(new EmbedRequest
            {
                Model = _options.EmbeddingModel,
                Input = texts.ToList()
            }, cancellationToken);

            if (response.Embeddings.Count != texts.Count)
            {
                throw new InvalidOperationException(
                    $"Ollama returned {response.Embeddings.Count} embeddings but expected {texts.Count}");
            }

            return response.Embeddings.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embeddings for {Count} texts", texts.Count);
            throw;
        }
    }

    /// <summary>
    /// Check if the embedding service is available by testing a small embedding.
    /// This works with both Ollama and MLX servers regardless of model name.
    /// </summary>
    public async Task<bool> IsModelAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Functional test: try to generate a small embedding
            EmbedResponse response = await _client.EmbedAsync(new EmbedRequest
            {
                Model = _options.EmbeddingModel,
                Input = ["test"]
            }, cancellationToken);

            return response.Embeddings.Count > 0 && response.Embeddings[0].Length > 0;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Embedding service test failed");
            return false;
        }
    }

    /// <summary>
    /// Ensure the embedding service is ready. For Ollama, pulls the model if needed.
    /// For MLX or other servers, just verifies embeddings work.
    /// </summary>
    public async Task EnsureModelAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (await IsModelAvailableAsync(cancellationToken))
        {
            _logger.LogDebug("Embedding service is ready");
            return;
        }

        // Only attempt pull for Ollama (port 11434)
        // MLX and other servers don't support model pulling
        if (_options.OllamaUrl.Contains(":11434"))
        {
            _logger.LogInformation("Pulling model {Model}...", _options.EmbeddingModel);

            await foreach (PullModelResponse? status in _client.PullModelAsync(_options.EmbeddingModel, cancellationToken))
            {
                if (status?.Status is { Length: > 0 } statusText)
                {
                    _logger.LogDebug("Pull status: {Status}", statusText);
                }
            }

            _logger.LogInformation("Model {Model} pulled successfully", _options.EmbeddingModel);
        }
        else
        {
            throw new InvalidOperationException(
                $"Embedding service at {_options.OllamaUrl} is not responding. " +
                "Ensure the MLX server is running: cd mlx-server && .venv/bin/python server.py --port 11435");
        }
    }

    /// <summary>
    /// Get the vector dimension for the configured embedding model.
    /// </summary>
    public int GetVectorDimension() => _options.VectorDimension;
}
