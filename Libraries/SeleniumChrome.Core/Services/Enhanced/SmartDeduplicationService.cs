using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SeleniumChrome.Core.Models;

namespace SeleniumChrome.Core.Services.Enhanced;

/// <summary>
/// Phase 2 Enhancement: Smart Deduplication Service
/// Provides intelligent duplicate detection across multiple job sources
/// </summary>
public class SmartDeduplicationService(ILogger<SmartDeduplicationService> logger)
{
    /// <summary>
    /// Detect and remove duplicate jobs across multiple sources
    /// </summary>
    public async Task<DeduplicationResult> DeduplicateJobsAsync(List<EnhancedJobListing> jobs)
    {
        logger.LogInformation($"Starting deduplication analysis for {jobs.Count} jobs");

        var result = new DeduplicationResult
        {
            OriginalCount = jobs.Count,
            ProcessedAt = DateTime.UtcNow
        };

        // Group jobs for duplicate detection
        var duplicateGroups = new List<List<EnhancedJobListing>>();
        var processedJobs = new HashSet<string>();
        
        foreach (EnhancedJobListing job in jobs)
        {
            string jobIdentifier = GetJobIdentifier(job);
            if (processedJobs.Contains(jobIdentifier))
                continue;

            List<EnhancedJobListing> duplicates = await FindDuplicatesAsync(job, jobs);
            if (duplicates.Count > 1)
            {
                duplicateGroups.Add(duplicates);
                foreach (EnhancedJobListing dup in duplicates)
                {
                    processedJobs.Add(GetJobIdentifier(dup));
                }
            }
            else
            {
                processedJobs.Add(jobIdentifier);
            }
        }

        // Select best representative from each duplicate group
        var uniqueJobs = new List<EnhancedJobListing>();
        
        foreach (List<EnhancedJobListing> group in duplicateGroups)
        {
            EnhancedJobListing bestJob = SelectBestJobFromGroup(group);
            bestJob.DuplicateInfo = new DuplicateJobInfo
            {
                IsDeduplicated = true,
                DuplicateCount = group.Count - 1,
                AlternateSources = group.Where(j => j.JobId != bestJob.JobId)
                    .Select(j => j.SourceSite.ToString()).ToList(),
                DuplicateUrls = group.Where(j => j.JobId != bestJob.JobId)
                    .Select(j => j.JobUrl).ToList()
            };
            uniqueJobs.Add(bestJob);
            
            result.DuplicateGroups.Add(new DuplicateGroup
            {
                SelectedJob = bestJob,
                Duplicates = group.Where(j => j.JobId != bestJob.JobId).ToList(),
                MatchReasons = GetMatchReasons(group)
            });
        }

        // Add jobs that weren't duplicates
        List<EnhancedJobListing> singleJobs = jobs.Where(j => !duplicateGroups.Any(g => g.Any(dj => dj.JobId == j.JobId))).ToList();
        uniqueJobs.AddRange(singleJobs);

        result.UniqueJobs = uniqueJobs;
        result.UniqueCount = uniqueJobs.Count;
        result.DuplicatesRemoved = result.OriginalCount - result.UniqueCount;
        result.DeduplicationRate = Math.Round((double)result.DuplicatesRemoved / result.OriginalCount * 100, 2);

        logger.LogInformation($"Deduplication complete: {result.DuplicatesRemoved} duplicates removed ({result.DeduplicationRate}%)");

        return result;
    }

    /// <summary>
    /// Find all jobs that are duplicates of the given job
    /// </summary>
    private async Task<List<EnhancedJobListing>> FindDuplicatesAsync(EnhancedJobListing targetJob, List<EnhancedJobListing> allJobs)
    {
        var duplicates = new List<EnhancedJobListing> { targetJob };

        foreach (EnhancedJobListing job in allJobs)
        {
            // Skip if it's the same job instance (identical object reference or same ID)
            if (IsSameJob(targetJob, job)) continue;

            if (await AreJobsDuplicatesAsync(targetJob, job))
            {
                duplicates.Add(job);
            }
        }

        return duplicates;
    }

    /// <summary>
    /// Get a unique identifier for a job for tracking purposes
    /// </summary>
    private string GetJobIdentifier(EnhancedJobListing job)
    {
        // Try different sources of unique identification
        if (!string.IsNullOrEmpty(job.JobId))
            return job.JobId;
        
        if (!string.IsNullOrEmpty(job.Id))
            return job.Id;
        
        if (!string.IsNullOrEmpty(job.JobUrl))
            return job.JobUrl;
        
        // Fall back to a combination of fields
        return $"{job.Company}|{job.Title}|{job.Location}".GetHashCode().ToString();
    }

    /// <summary>
    /// Check if two jobs are the same job (identity check)
    /// </summary>
    private bool IsSameJob(EnhancedJobListing job1, EnhancedJobListing job2)
    {
        // If both have JobIds and they're not empty, use that
        if (!string.IsNullOrEmpty(job1.JobId) && !string.IsNullOrEmpty(job2.JobId))
        {
            return job1.JobId == job2.JobId;
        }

        // If both have Ids and they're not empty, use that
        if (!string.IsNullOrEmpty(job1.Id) && !string.IsNullOrEmpty(job2.Id))
        {
            return job1.Id == job2.Id;
        }

        // Fall back to URL comparison if available
        if (!string.IsNullOrEmpty(job1.JobUrl) && !string.IsNullOrEmpty(job2.JobUrl))
        {
            return job1.JobUrl == job2.JobUrl;
        }

        // If no unique identifiers, use reference equality
        return ReferenceEquals(job1, job2);
    }

    /// <summary>
    /// Determine if two jobs are duplicates using multiple criteria
    /// </summary>
    private async Task<bool> AreJobsDuplicatesAsync(EnhancedJobListing job1, EnhancedJobListing job2)
    {
        // URL-based matching (highest confidence)
        if (AreUrlsDuplicate(job1.JobUrl, job2.JobUrl))
            return true;

        // Company + Title matching (high confidence)
        if (AreCompaniesMatching(job1.Company, job2.Company) && 
            AreTitlesMatching(job1.Title, job2.Title))
            return true;

        // Salary + Location + Title matching (medium confidence)
        if (AreSalariesMatching(job1.Salary, job2.Salary) &&
            AreLocationsMatching(job1.Location, job2.Location) &&
            AreTitlesMatching(job1.Title, job2.Title))
            return true;

        // Description similarity (lower confidence, more expensive)
        if (AreCompaniesMatching(job1.Company, job2.Company) &&
            GetDescriptionSimilarity(job1.Description, job2.Description) > 0.85)
            return true;

        return false;
    }

    /// <summary>
    /// Check if URLs point to the same job posting
    /// </summary>
    private bool AreUrlsDuplicate(string url1, string url2)
    {
        if (string.IsNullOrEmpty(url1) || string.IsNullOrEmpty(url2))
            return false;

        // Normalize URLs
        string normalized1 = NormalizeUrl(url1);
        string normalized2 = NormalizeUrl(url2);

        if (normalized1 == normalized2)
            return true;

        // Check for redirects or URL variations
        return CheckUrlVariations(normalized1, normalized2);
    }

    /// <summary>
    /// Normalize URL for comparison
    /// </summary>
    private string NormalizeUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return string.Empty;

        try
        {
            var uri = new Uri(url.ToLower());
            
            // Remove common tracking parameters
            string[] cleanQuery = uri.Query
                .Replace("?", "")
                .Split('&')
                .Where(param => !IsTrackingParameter(param))
                .ToArray();

            string cleanQueryString = cleanQuery.Length > 0 ? "?" + string.Join("&", cleanQuery) : "";
            
            return $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}{cleanQueryString}";
        }
        catch
        {
            return url.ToLower();
        }
    }

    private bool IsTrackingParameter(string param)
    {
        string[] trackingParams = ["utm_", "ref=", "source=", "campaign=", "medium=", "gclid=", "fbclid="];
        return trackingParams.Any(tp => param.StartsWith(tp, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Check for URL variations that might represent the same job
    /// </summary>
    private bool CheckUrlVariations(string url1, string url2)
    {
        // Extract job IDs from URLs
        string jobId1 = ExtractJobIdFromUrl(url1);
        string jobId2 = ExtractJobIdFromUrl(url2);

        if (!string.IsNullOrEmpty(jobId1) && !string.IsNullOrEmpty(jobId2))
        {
            return jobId1 == jobId2;
        }

        return false;
    }

    /// <summary>
    /// Extract job ID from URL using common patterns
    /// </summary>
    private string ExtractJobIdFromUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return string.Empty;

        // Common job ID patterns
        string[] patterns =
        [
            @"/jobs/(\d+)",           // /jobs/123456
            @"/job/(\d+)",            // /job/123456
            @"jobId[=:]([^&\s]+)",    // jobId=abc123
            @"id[=:]([^&\s]+)",       // id=abc123
            @"/([a-f0-9-]{36})",      // UUID pattern
            @"/([a-f0-9]{24})"        // MongoDB ObjectId pattern
        ];

        foreach (string pattern in patterns)
        {
            Match match = Regex.Match(url, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Check if company names represent the same company
    /// </summary>
    private bool AreCompaniesMatching(string company1, string company2)
    {
        if (string.IsNullOrEmpty(company1) || string.IsNullOrEmpty(company2))
            return false;

        string normalized1 = NormalizeCompanyName(company1);
        string normalized2 = NormalizeCompanyName(company2);

        return normalized1 == normalized2 || GetStringSimilarity(normalized1, normalized2) > 0.9;
    }

    /// <summary>
    /// Normalize company name for comparison
    /// </summary>
    private string NormalizeCompanyName(string companyName)
    {
        if (string.IsNullOrEmpty(companyName)) return string.Empty;

        string normalized = companyName.ToLower().Trim();

        // Remove common corporate suffixes
        string[] suffixes = [", inc.", ", inc", ", llc", ", ltd", ", corp", ", corporation", " inc", " llc", " ltd"];
        foreach (string suffix in suffixes)
        {
            if (normalized.EndsWith(suffix))
            {
                normalized = normalized.Substring(0, normalized.Length - suffix.Length);
                break;
            }
        }

        // Remove extra whitespace
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();

        return normalized;
    }

    /// <summary>
    /// Check if job titles are similar enough to be the same position
    /// </summary>
    private bool AreTitlesMatching(string title1, string title2)
    {
        if (string.IsNullOrEmpty(title1) || string.IsNullOrEmpty(title2))
            return false;

        string normalized1 = NormalizeJobTitle(title1);
        string normalized2 = NormalizeJobTitle(title2);

        return GetStringSimilarity(normalized1, normalized2) > 0.8;
    }

    /// <summary>
    /// Normalize job title for comparison
    /// </summary>
    private string NormalizeJobTitle(string title)
    {
        if (string.IsNullOrEmpty(title)) return string.Empty;

        string normalized = title.ToLower().Trim();

        // Remove common variations
        normalized = Regex.Replace(normalized, @"\b(sr|senior|jr|junior)\b\.?", "");
        normalized = Regex.Replace(normalized, @"\s+", " ");
        normalized = normalized.Trim();

        return normalized;
    }

    /// <summary>
    /// Check if salaries are similar (indicating same job posting)
    /// </summary>
    private bool AreSalariesMatching(string salary1, string salary2)
    {
        if (string.IsNullOrEmpty(salary1) || string.IsNullOrEmpty(salary2))
            return false;

        // Extract numeric values
        List<decimal> nums1 = ExtractSalaryNumbers(salary1);
        List<decimal> nums2 = ExtractSalaryNumbers(salary2);

        if (nums1.Count == 0 || nums2.Count == 0)
            return false;

        // Check if ranges overlap significantly
        return CheckSalaryRangeOverlap(nums1, nums2);
    }

    private List<decimal> ExtractSalaryNumbers(string salary)
    {
        var numbers = new List<decimal>();
        MatchCollection matches = Regex.Matches(salary.Replace(",", ""), @"\d+");
        
        foreach (Match match in matches)
        {
            if (decimal.TryParse(match.Value, out decimal num))
            {
                numbers.Add(num);
            }
        }

        return numbers;
    }

    private bool CheckSalaryRangeOverlap(List<decimal> range1, List<decimal> range2)
    {
        if (range1.Count == 0 || range2.Count == 0) return false;

        decimal min1 = range1.Min();
        decimal max1 = range1.Max();
        decimal min2 = range2.Min();
        decimal max2 = range2.Max();

        // Check for significant overlap (at least 50%)
        decimal overlapStart = Math.Max(min1, min2);
        decimal overlapEnd = Math.Min(max1, max2);
        decimal overlap = Math.Max(0, overlapEnd - overlapStart);
        
        decimal range1Size = max1 - min1;
        decimal range2Size = max2 - min2;
        decimal avgRangeSize = (range1Size + range2Size) / 2;

        return avgRangeSize > 0 && overlap / avgRangeSize > 0.5m;
    }

    /// <summary>
    /// Check if locations represent the same area
    /// </summary>
    private bool AreLocationsMatching(string location1, string location2)
    {
        if (string.IsNullOrEmpty(location1) || string.IsNullOrEmpty(location2))
            return false;

        string normalized1 = NormalizeLocation(location1);
        string normalized2 = NormalizeLocation(location2);

        return normalized1 == normalized2 || GetStringSimilarity(normalized1, normalized2) > 0.8;
    }

    private string NormalizeLocation(string location)
    {
        if (string.IsNullOrEmpty(location)) return string.Empty;

        string normalized = location.ToLower().Trim();
        
        // Handle remote work variations
        if (normalized.Contains("remote"))
        {
            return "remote";
        }

        // Remove state/country for city matching
        normalized = Regex.Replace(normalized, @",.*", "").Trim();
        
        return normalized;
    }

    /// <summary>
    /// Calculate description similarity (expensive operation, used sparingly)
    /// </summary>
    private double GetDescriptionSimilarity(string desc1, string desc2)
    {
        if (string.IsNullOrEmpty(desc1) || string.IsNullOrEmpty(desc2))
            return 0.0;

        // Use a simplified similarity check for performance
        HashSet<string> words1 = GetSignificantWords(desc1);
        HashSet<string> words2 = GetSignificantWords(desc2);

        if (words1.Count == 0 || words2.Count == 0)
            return 0.0;

        int intersection = words1.Intersect(words2).Count();
        int union = words1.Union(words2).Count();

        return (double)intersection / union; // Jaccard similarity
    }

    private HashSet<string> GetSignificantWords(string text)
    {
        if (string.IsNullOrEmpty(text)) return [];

        HashSet<string> words = Regex.Matches(text.ToLower(), @"\b\w{4,}\b")
            .Select(m => m.Value)
            .Where(w => !IsStopWord(w))
            .ToHashSet();

        return words;
    }

    private bool IsStopWord(string word)
    {
        var stopWords = new HashSet<string> { "the", "and", "you", "will", "with", "this", "that", "have", "from", "they", "been", "your", "work", "team", "role" };
        return stopWords.Contains(word);
    }

    /// <summary>
    /// Calculate string similarity using Levenshtein distance
    /// </summary>
    private double GetStringSimilarity(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2))
            return 0.0;

        if (s1 == s2) return 1.0;

        int distance = LevenshteinDistance(s1, s2);
        int maxLength = Math.Max(s1.Length, s2.Length);
        
        return 1.0 - (double)distance / maxLength;
    }

    private int LevenshteinDistance(string s1, string s2)
    {
        var matrix = new int[s1.Length + 1, s2.Length + 1];

        for (var i = 0; i <= s1.Length; i++)
            matrix[i, 0] = i;

        for (var j = 0; j <= s2.Length; j++)
            matrix[0, j] = j;

        for (var i = 1; i <= s1.Length; i++)
        {
            for (var j = 1; j <= s2.Length; j++)
            {
                int cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[s1.Length, s2.Length];
    }

    /// <summary>
    /// Select the best job from a group of duplicates
    /// </summary>
    private EnhancedJobListing SelectBestJobFromGroup(List<EnhancedJobListing> duplicates)
    {
        if (duplicates.Count == 1) return duplicates[0];

        // Score each job based on data completeness and source reliability
        var scored = duplicates.Select(job => new
        {
            Job = job,
            Score = CalculateJobQualityScore(job)
        }).OrderByDescending(x => x.Score);

        return scored.First().Job;
    }

    /// <summary>
    /// Calculate job quality score for selection from duplicates
    /// </summary>
    private int CalculateJobQualityScore(EnhancedJobListing job)
    {
        var score = 0;

        // Data completeness
        if (!string.IsNullOrEmpty(job.Description)) score += 20;
        if (!string.IsNullOrEmpty(job.Salary)) score += 15;
        if (!string.IsNullOrEmpty(job.Company)) score += 10;
        if (!string.IsNullOrEmpty(job.Location)) score += 10;
        if (job.Requirements?.Any() == true) score += 10;

        // Source reliability (prefer direct company sources)
        score += job.SourceSite switch
        {
            JobSite.CompanyCareerPage => 20,
            JobSite.LinkedIn => 15,
            JobSite.SimplifyJobs => 12,
            JobSite.Indeed => 10,
            JobSite.Glassdoor => 8,
            _ => 5
        };

        // Recency
        if (job.PostedDate.HasValue)
        {
            double daysOld = (DateTime.UtcNow - job.PostedDate.Value).TotalDays;
            if (daysOld < 7) score += 10;
            else if (daysOld < 30) score += 5;
        }

        return score;
    }

    /// <summary>
    /// Get reasons why jobs were considered duplicates
    /// </summary>
    private List<string> GetMatchReasons(List<EnhancedJobListing> group)
    {
        var reasons = new List<string>();

        if (group.Count < 2) return reasons;

        EnhancedJobListing first = group[0];
        IEnumerable<EnhancedJobListing> rest = group.Skip(1);

        foreach (EnhancedJobListing job in rest)
        {
            if (AreUrlsDuplicate(first.JobUrl, job.JobUrl))
                reasons.Add($"URL match: {first.SourceSite} vs {job.SourceSite}");
            else if (AreCompaniesMatching(first.Company, job.Company) && AreTitlesMatching(first.Title, job.Title))
                reasons.Add($"Company + Title match: {first.SourceSite} vs {job.SourceSite}");
            else
                reasons.Add($"Content similarity: {first.SourceSite} vs {job.SourceSite}");
        }

        return reasons.Distinct().ToList();
    }
}

/// <summary>
/// Result of deduplication analysis
/// </summary>
public class DeduplicationResult
{
    public int OriginalCount { get; set; }
    public int UniqueCount { get; set; }
    public int DuplicatesRemoved { get; set; }
    public double DeduplicationRate { get; set; }
    public List<EnhancedJobListing> UniqueJobs { get; set; } = [];
    public List<DuplicateGroup> DuplicateGroups { get; set; } = [];
    public DateTime ProcessedAt { get; set; }
}

/// <summary>
/// Information about a group of duplicate jobs
/// </summary>
public class DuplicateGroup
{
    public EnhancedJobListing SelectedJob { get; set; } = null!;
    public List<EnhancedJobListing> Duplicates { get; set; } = [];
    public List<string> MatchReasons { get; set; } = [];
}

/// <summary>
/// Information about duplicate jobs attached to the primary job
/// </summary>
public class DuplicateJobInfo
{
    public bool IsDeduplicated { get; set; }
    public int DuplicateCount { get; set; }
    public List<string> AlternateSources { get; set; } = [];
    public List<string> DuplicateUrls { get; set; } = [];
}
