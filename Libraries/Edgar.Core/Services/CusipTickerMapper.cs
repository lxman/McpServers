using System.Text.Json;
using Edgar.Core.Models;
using Mcp.Common.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Edgar.Core.Services;

public class CusipTickerMapper(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<CusipTickerMapper> logger)
{
    private const string OpenFigiUrl = "https://api.openfigi.com/v3/mapping";
    private Dictionary<string, string>? _cache;
    private string CacheFilePath => Path.Combine(
        configuration["Edgar:DataDirectory"] ?? "./data",
        "cusip_ticker_cache.json");

    public async Task<string?> GetTickerAsync(string cusip, CancellationToken ct = default)
    {
        Dictionary<string, string> cache = await LoadCacheAsync(ct);

        // Check manual overrides first — they always win over cache
        IConfigurationSection overrides = configuration.GetSection("Edgar:CusipOverrides");
        string? manualTicker = overrides[cusip];
        if (!string.IsNullOrEmpty(manualTicker))
        {
            if (!cache.TryGetValue(cusip, out string? existing) || existing != manualTicker)
            {
                cache[cusip] = manualTicker;
                await SaveCacheAsync(ct);
            }
            return manualTicker;
        }

        if (cache.TryGetValue(cusip, out string? cached))
            return cached;

        // Query OpenFIGI
        string? ticker = await LookupViaOpenFigiAsync(cusip, ct);
        if (ticker is not null)
        {
            cache[cusip] = ticker;
            await SaveCacheAsync(ct);
        }

        return ticker;
    }

    public async Task MapHoldingsAsync(List<Holding> holdings, CancellationToken ct = default)
    {
        foreach (Holding holding in holdings)
        {
            if (!string.IsNullOrEmpty(holding.Ticker)) continue;
            holding.Ticker = await GetTickerAsync(holding.Cusip, ct);
        }
    }

    public async Task MapChangesAsync(HoldingsDiff diff, CancellationToken ct = default)
    {
        IEnumerable<HoldingChange> allChanges = diff.NewPositions
            .Concat(diff.IncreasedPositions)
            .Concat(diff.DecreasedPositions)
            .Concat(diff.ExitedPositions)
            .Concat(diff.UnchangedPositions);

        foreach (HoldingChange change in allChanges)
        {
            if (!string.IsNullOrEmpty(change.Ticker)) continue;
            change.Ticker = await GetTickerAsync(change.Cusip, ct);
        }
    }

    private async Task<string?> LookupViaOpenFigiAsync(string cusip, CancellationToken ct)
    {
        try
        {
            HttpClient client = httpClientFactory.CreateClient("OpenFigi");
            string requestBody = JsonSerializer.Serialize(new[]
            {
                new { idType = "ID_CUSIP", idValue = cusip }
            });

            var request = new HttpRequestMessage(HttpMethod.Post, OpenFigiUrl)
            {
                Content = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json")
            };

            HttpResponseMessage response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("OpenFIGI returned {StatusCode} for CUSIP {Cusip}",
                    response.StatusCode, cusip);
                return null;
            }

            string json = await response.Content.ReadAsStringAsync(ct);
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            if (root.GetArrayLength() == 0) return null;

            JsonElement first = root[0];
            if (first.TryGetProperty("data", out JsonElement data) && data.GetArrayLength() > 0)
            {
                string? ticker = data[0].GetProperty("ticker").GetString();
                logger.LogInformation("Resolved CUSIP {Cusip} -> {Ticker}", cusip, ticker);
                return ticker;
            }

            if (first.TryGetProperty("error", out JsonElement error))
            {
                logger.LogWarning("OpenFIGI error for CUSIP {Cusip}: {Error}",
                    cusip, error.GetString());
            }

            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to lookup CUSIP {Cusip} via OpenFIGI", cusip);
            return null;
        }
    }

    private async Task<Dictionary<string, string>> LoadCacheAsync(CancellationToken ct)
    {
        if (_cache is not null) return _cache;

        if (File.Exists(CacheFilePath))
        {
            try
            {
                string json = await File.ReadAllTextAsync(CacheFilePath, ct);
                _cache = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load CUSIP cache, starting fresh");
                _cache = [];
            }
        }
        else
        {
            _cache = [];
        }

        return _cache;
    }

    private async Task SaveCacheAsync(CancellationToken ct)
    {
        if (_cache is null) return;

        try
        {
            string? dir = Path.GetDirectoryName(CacheFilePath);
            if (dir is not null) Directory.CreateDirectory(dir);

            string json = JsonSerializer.Serialize(_cache, SerializerOptions.JsonOptionsIndented);
            await File.WriteAllTextAsync(CacheFilePath, json, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to save CUSIP cache");
        }
    }
}
