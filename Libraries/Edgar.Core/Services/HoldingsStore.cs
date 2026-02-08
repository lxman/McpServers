using System.Text.Json;
using Edgar.Core.Models;
using Mcp.Common.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Edgar.Core.Services;

public class HoldingsStore(
    IConfiguration configuration,
    ILogger<HoldingsStore> logger)
{
    private string DataDirectory => configuration["Edgar:DataDirectory"] ?? "./data";

    public async Task SaveHoldingsAsync(
        string filerName, DateOnly reportDate, List<Holding> holdings,
        CancellationToken ct = default)
    {
        string dir = GetFilerDirectory(filerName);
        Directory.CreateDirectory(dir);

        string filePath = Path.Combine(dir, $"holdings_{reportDate:yyyy-MM-dd}.json");
        string json = JsonSerializer.Serialize(holdings, SerializerOptions.JsonOptionsIndented);
        await File.WriteAllTextAsync(filePath, json, ct);

        logger.LogInformation("Saved {Count} holdings for {Filer} ({Date}) to {Path}",
            holdings.Count, filerName, reportDate, filePath);
    }

    public async Task<List<Holding>?> LoadHoldingsAsync(
        string filerName, DateOnly reportDate,
        CancellationToken ct = default)
    {
        string filePath = Path.Combine(
            GetFilerDirectory(filerName),
            $"holdings_{reportDate:yyyy-MM-dd}.json");

        if (!File.Exists(filePath))
        {
            logger.LogDebug("No cached holdings for {Filer} ({Date})", filerName, reportDate);
            return null;
        }

        try
        {
            string json = await File.ReadAllTextAsync(filePath, ct);
            return JsonSerializer.Deserialize<List<Holding>>(json);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load cached holdings from {Path}", filePath);
            return null;
        }
    }

    public List<DateOnly> ListAvailableDates(string filerName)
    {
        string dir = GetFilerDirectory(filerName);
        if (!Directory.Exists(dir)) return [];

        return Directory.GetFiles(dir, "holdings_*.json")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .Select(n => n.Replace("holdings_", ""))
            .Select(d => DateOnly.TryParse(d, out DateOnly date) ? date : (DateOnly?)null)
            .Where(d => d.HasValue)
            .Select(d => d!.Value)
            .OrderDescending()
            .ToList();
    }

    private string GetFilerDirectory(string filerName)
    {
        string safeName = filerName.Replace(" ", "_").ToLowerInvariant();
        return Path.Combine(DataDirectory, safeName);
    }
}
