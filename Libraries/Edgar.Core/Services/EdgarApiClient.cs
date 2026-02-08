using System.Text.Json;
using Edgar.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Edgar.Core.Services;

public class EdgarApiClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<EdgarApiClient> logger)
{
    private const string SubmissionsBaseUrl = "https://data.sec.gov/submissions/CIK{0}.json";
    private const string ArchivesBaseUrl = "https://www.sec.gov/Archives/edgar/data/{0}/{1}/";

    private List<TrackedFiler>? _trackedFilers;

    public List<TrackedFiler> GetTrackedFilers()
    {
        if (_trackedFilers is not null) return _trackedFilers;

        _trackedFilers = configuration.GetSection("Edgar:TrackedFilers").Get<List<TrackedFiler>>() ?? [];
        return _trackedFilers;
    }

    public TrackedFiler? FindFiler(string filerName)
    {
        return GetTrackedFilers()
            .FirstOrDefault(f => f.Name.Equals(filerName, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<Filing13F?> GetLatestFilingAsync(string filerName, CancellationToken ct = default)
    {
        List<Filing13F> filings = await ListFilingsAsync(filerName, 1, ct);
        return filings.FirstOrDefault();
    }

    public async Task<List<Filing13F>> ListFilingsAsync(string filerName, int count = 10, CancellationToken ct = default)
    {
        TrackedFiler filer = FindFiler(filerName)
                             ?? throw new ArgumentException($"Filer '{filerName}' is not in the tracked filers list.");

        JsonDocument submissionsJson = await FetchSubmissionsAsync(filer.PaddedCik, ct);
        return ParseFilingsFromSubmissions(submissionsJson, filer, count);
    }

    public async Task<string> FetchInformationTableXmlAsync(Filing13F filing, CancellationToken ct = default)
    {
        HttpClient client = CreateClient();
        string cik = filing.Cik.TrimStart('0');

        // First, get the filing index to find the informationTable XML document
        string indexUrl = string.Format(ArchivesBaseUrl, cik, filing.AccessionNumberForUrl);
        logger.LogDebug("Fetching filing index from {Url}", indexUrl);

        string indexResponse = await client.GetStringAsync(indexUrl + "index.json", ct);
        using JsonDocument indexDoc = JsonDocument.Parse(indexResponse);

        string? infoTableFile = null;
        var primaryDocName = Path.GetFileName(filing.PrimaryDocument);
        if (indexDoc.RootElement.TryGetProperty("directory", out JsonElement directory) &&
            directory.TryGetProperty("item", out JsonElement items))
        {
            foreach (JsonElement item in items.EnumerateArray())
            {
                string name = item.GetProperty("name").GetString() ?? "";

                // First priority: filename contains "infotable"
                if (name.Contains("infotable", StringComparison.OrdinalIgnoreCase) &&
                    name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    infoTableFile = name;
                    break;
                }

                // Second priority: any .xml file that isn't the primary doc (cover page)
                // and isn't an index file. The info table is the "other" XML.
                if (infoTableFile is null &&
                    name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) &&
                    !name.Equals(primaryDocName, StringComparison.OrdinalIgnoreCase) &&
                    !name.Contains("index", StringComparison.OrdinalIgnoreCase))
                {
                    infoTableFile = name;
                    // Don't break — keep looking for an explicit "infotable" match
                }
            }
        }

        if (infoTableFile is null)
        {
            throw new InvalidOperationException(
                $"Could not find informationTable XML in filing {filing.AccessionNumber}. " +
                "This may not be a 13F-HR filing with holdings data.");
        }

        string xmlUrl = indexUrl + infoTableFile;
        logger.LogInformation("Fetching informationTable from {Url}", xmlUrl);
        return await client.GetStringAsync(xmlUrl, ct);
    }

    private async Task<JsonDocument> FetchSubmissionsAsync(string paddedCik, CancellationToken ct)
    {
        HttpClient client = CreateClient();
        string url = string.Format(SubmissionsBaseUrl, paddedCik);

        logger.LogDebug("Fetching submissions from {Url}", url);
        string response = await client.GetStringAsync(url, ct);
        return JsonDocument.Parse(response);
    }

    private static List<Filing13F> ParseFilingsFromSubmissions(
        JsonDocument submissionsJson, TrackedFiler filer, int count)
    {
        var filings = new List<Filing13F>();
        JsonElement root = submissionsJson.RootElement;

        if (!root.TryGetProperty("filings", out JsonElement filingsElement) ||
            !filingsElement.TryGetProperty("recent", out JsonElement recent))
        {
            return filings;
        }

        JsonElement forms = recent.GetProperty("form");
        JsonElement filingDates = recent.GetProperty("filingDate");
        JsonElement reportDates = recent.GetProperty("reportDate");
        JsonElement accessionNumbers = recent.GetProperty("accessionNumber");
        JsonElement primaryDocuments = recent.GetProperty("primaryDocument");

        int totalFilings = forms.GetArrayLength();
        int found = 0;

        for (int i = 0; i < totalFilings && found < count; i++)
        {
            string form = forms[i].GetString() ?? "";
            if (!form.Equals("13F-HR", StringComparison.OrdinalIgnoreCase) &&
                !form.Equals("13F-HR/A", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            filings.Add(new Filing13F
            {
                FilerName = filer.Name,
                Cik = filer.Cik,
                FormType = form,
                FilingDate = DateOnly.Parse(filingDates[i].GetString() ?? ""),
                ReportDate = DateOnly.Parse(reportDates[i].GetString() ?? ""),
                AccessionNumber = accessionNumbers[i].GetString() ?? "",
                PrimaryDocument = primaryDocuments[i].GetString() ?? ""
            });
            found++;
        }

        return filings;
    }

    private HttpClient CreateClient()
    {
        HttpClient client = httpClientFactory.CreateClient("Edgar");
        string userAgent = configuration["Edgar:UserAgent"] ?? "McpServers jordan.mymail@gmail.com";
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", userAgent);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        return client;
    }
}
