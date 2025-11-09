using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SeleniumChrome.Core.Models;

namespace SeleniumChrome.Core.Services.Enhanced;

/// <summary>
/// Phase 1 Enhancement: Intelligent scoring algorithm based on 50 years experience and .NET specialization
/// </summary>
public class NetDeveloperJobScorer(ILogger<NetDeveloperJobScorer> logger)
{
    /// <summary>
    /// Calculate enhanced match score with detailed breakdown
    /// </summary>
    public JobScoringResult CalculateEnhancedMatchScore(EnhancedJobListing job, NetDeveloperScoringProfile profile)
    {
        var result = new JobScoringResult();

        try
        {
            // Calculate individual score components
            result.SalaryScore = CalculateSalaryScore(job, profile.SalaryPrefs);
            result.TechnologyScore = CalculateTechnologyScore(job, profile.TechPrefs);
            result.CompanyScore = CalculateCompanyScore(job, profile.CompanyPrefs);
            result.RemoteScore = CalculateRemoteScore(job, profile.WorkPrefs);
            result.ExperienceScore = CalculateExperienceScore(job, profile.ExperiencePrefs);

            // Calculate weighted total score
            result.TotalScore = (int)Math.Round(
                (result.SalaryScore * profile.SalaryPrefs.Weight) +
                (result.TechnologyScore * profile.TechPrefs.Weight) +
                (result.CompanyScore * profile.CompanyPrefs.Weight) +
                (result.RemoteScore * profile.WorkPrefs.Weight) +
                (result.ExperienceScore * profile.ExperiencePrefs.Weight)
            );

            result.TotalScore = Math.Max(0, Math.Min(100, result.TotalScore)); // Clamp to 0-100

            logger.LogDebug($"Scored job '{job.Title}' at '{job.Company}': {result.TotalScore}% " +
                           $"(S:{result.SalaryScore}, T:{result.TechnologyScore}, C:{result.CompanyScore}, " +
                           $"R:{result.RemoteScore}, E:{result.ExperienceScore})");

            return result;
        }
        catch (Exception ex)
        {
            logger.LogWarning($"Error calculating job score for '{job.Title}': {ex.Message}");
            return new JobScoringResult { TotalScore = 30 }; // Default fallback score
        }
    }

    private int CalculateSalaryScore(EnhancedJobListing job, SalaryPreferences prefs)
    {
        int? salary = ExtractSalaryFromJob(job);
        
        if (salary is null)
            return 50; // Neutral score if no salary info

        if (salary >= prefs.TargetSalary)
            return 100; // Perfect score for target+ salary
        
        if (salary >= prefs.PreferredSalary)
            return 80; // High score for preferred+ salary
        
        if (salary >= prefs.MinSalary)
            return 60; // Acceptable score for minimum+ salary
        
        // Linear scaling below minimum
        double ratio = (double)salary / prefs.MinSalary;
        return (int)(ratio * 60);
    }

    private int CalculateTechnologyScore(EnhancedJobListing job, TechnologyPreferences prefs)
    {
        var score = 0;
        string jobText = GetJobTextForAnalysis(job).ToLower();
        List<string> technologies = job.Technologies?.Select(t => t.ToLower()).ToList() ?? [];

        // Core technologies (required) - 50 points max
        var coreMatches = 0;
        foreach (string tech in prefs.CoreTechnologies)
        {
            if (jobText.Contains(tech.ToLower()) || technologies.Any(t => t.Contains(tech.ToLower())))
            {
                coreMatches++;
            }
        }
        score += (int)((double)coreMatches / prefs.CoreTechnologies.Count * 50);

        // Preferred technologies - 30 points max
        var preferredMatches = 0;
        foreach (string tech in prefs.PreferredTechnologies)
        {
            if (jobText.Contains(tech.ToLower()) || technologies.Any(t => t.Contains(tech.ToLower())))
            {
                preferredMatches++;
            }
        }
        score += (int)((double)preferredMatches / prefs.PreferredTechnologies.Count * 30);

        // Bonus technologies - 20 points max
        var bonusMatches = 0;
        foreach (string tech in prefs.BonusTechnologies)
        {
            if (jobText.Contains(tech.ToLower()) || technologies.Any(t => t.Contains(tech.ToLower())))
            {
                bonusMatches++;
            }
        }
        score += Math.Min(20, bonusMatches * 5); // 5 points per bonus tech, max 20

        return Math.Min(100, score);
    }

    private int CalculateCompanyScore(EnhancedJobListing job, CompanyPreferences prefs)
    {
        var score = 50; // Base score
        string companyInfo = (job.Notes ?? "").ToLower();
        string description = (job.Description ?? "").ToLower();

        // Company stage preferences
        foreach (string preferredStage in prefs.PreferredStages)
        {
            if (companyInfo.Contains(preferredStage.ToLower()) || description.Contains(preferredStage.ToLower()))
            {
                score += 20;
                break;
            }
        }

        // Penalize avoided stages
        foreach (string avoidStage in prefs.AvoidStages)
        {
            if (companyInfo.Contains(avoidStage.ToLower()) || description.Contains(avoidStage.ToLower()))
            {
                score -= 30;
                break;
            }
        }

        // Look for company size indicators
        Match sizeMatch = Regex.Match(companyInfo, @"(\d+)[-\s]*(\d+)?\s*employees?", RegexOptions.IgnoreCase);
        if (sizeMatch.Success)
        {
            if (int.TryParse(sizeMatch.Groups[1].Value, out int minSize))
            {
                int maxSize = sizeMatch.Groups[2].Success && int.TryParse(sizeMatch.Groups[2].Value, out int max) 
                    ? max : minSize;

                if (minSize >= prefs.MinEmployeeCount && maxSize <= prefs.MaxEmployeeCount)
                {
                    score += 15; // Bonus for preferred company size
                }
            }
        }

        // Look for growth/funding indicators
        string[] growthKeywords = ["funding", "series", "growth", "expanding", "scale", "venture"];
        if (growthKeywords.Any(keyword => companyInfo.Contains(keyword) || description.Contains(keyword)))
        {
            score += 10;
        }

        return Math.Max(0, Math.Min(100, score));
    }

    private int CalculateRemoteScore(EnhancedJobListing job, WorkPreferences prefs)
    {
        if (!prefs.PreferRemote)
            return 50; // Neutral if no remote preference

        string location = (job.Location ?? "").ToLower();
        string description = (job.Description ?? "").ToLower();

        // Perfect score for remote work
        if (job.IsRemote || location.Contains("remote") || description.Contains("remote"))
        {
            return 100;
        }

        // Check for acceptable locations
        foreach (string acceptableLocation in prefs.AcceptableLocations)
        {
            if (location.Contains(acceptableLocation.ToLower()))
            {
                return 80; // High score for acceptable locations
            }
        }

        // Check for hybrid/flexible work options
        string[] flexKeywords = ["hybrid", "flexible", "work from home", "wfh"];
        if (flexKeywords.Any(keyword => description.Contains(keyword)))
        {
            return 60;
        }

        return 20; // Low score for on-site only positions
    }

    private int CalculateExperienceScore(EnhancedJobListing job, ExperiencePreferences prefs)
    {
        string jobText = GetJobTextForAnalysis(job).ToLower();
        var score = 50; // Base score

        // Check for target experience levels
        foreach (string targetLevel in prefs.TargetLevels)
        {
            if (jobText.Contains(targetLevel.ToLower()))
            {
                score += 15;
                break; // Only count one target level match
            }
        }

        // Penalize avoided experience levels
        foreach (string avoidLevel in prefs.AvoidLevels)
        {
            if (jobText.Contains(avoidLevel.ToLower()))
            {
                score -= 40; // Heavy penalty for junior/entry roles
                break;
            }
        }

        // Look for years of experience requirements
        Match expMatch = Regex.Match(jobText, @"(\d+)\+?\s*years?\s*(?:of\s*)?experience", RegexOptions.IgnoreCase);
        if (expMatch.Success && int.TryParse(expMatch.Groups[1].Value, out int yearsRequired))
        {
            if (yearsRequired is >= 5 and <= 15)
            {
                score += 10; // Bonus for appropriate experience range
            }
            else if (yearsRequired < 3)
            {
                score -= 20; // Penalty for too junior
            }
            else if (yearsRequired > 20)
            {
                score -= 10; // Slight penalty for unrealistic requirements
            }
        }

        return Math.Max(0, Math.Min(100, score));
    }

    private int? ExtractSalaryFromJob(EnhancedJobListing job)
    {
        string textToSearch = GetJobTextForAnalysis(job);
        
        // Common salary patterns
        string[] patterns =
        [
            @"\$(\d{1,3}(?:,\d{3})*(?:\.\d{2})?)\s*-\s*\$(\d{1,3}(?:,\d{3})*(?:\.\d{2})?)", // $120,000 - $150,000
            @"\$(\d{1,3}(?:,\d{3})*(?:\.\d{2})?)\s*to\s*\$(\d{1,3}(?:,\d{3})*(?:\.\d{2})?)", // $120,000 to $150,000
            @"\$(\d{1,3}(?:,\d{3})*(?:\.\d{2})?)", // $120,000
            @"(\d{1,3})k\s*-\s*(\d{1,3})k", // 120k - 150k
            @"(\d{1,3})k" // 120k
        ];

        foreach (string pattern in patterns)
        {
            MatchCollection matches = Regex.Matches(textToSearch, pattern, RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                if (match.Groups.Count >= 2)
                {
                    string salaryText = match.Groups[1].Value.Replace(",", "");
                    if (int.TryParse(salaryText, out int salary))
                    {
                        // Convert k notation to full number
                        if (match.Value.Contains("k"))
                        {
                            salary *= 1000;
                        }
                        
                        // Return midpoint if range detected
                        if (match.Groups.Count >= 3 && !string.IsNullOrEmpty(match.Groups[2].Value))
                        {
                            string highText = match.Groups[2].Value.Replace(",", "");
                            if (int.TryParse(highText, out int high))
                            {
                                if (match.Value.Contains("k"))
                                {
                                    high *= 1000;
                                }
                                return (salary + high) / 2;
                            }
                        }
                        
                        return salary;
                    }
                }
            }
        }

        return null;
    }

    private string GetJobTextForAnalysis(EnhancedJobListing job)
    {
        return $"{job.Title} {job.Description} {job.Summary} {job.Notes} " +
               $"{string.Join(" ", job.Technologies ?? [])}";
    }
}

/// <summary>
/// Result of job scoring with detailed breakdown
/// </summary>
public class JobScoringResult
{
    public int TotalScore { get; set; }
    public int SalaryScore { get; set; }
    public int TechnologyScore { get; set; }
    public int CompanyScore { get; set; }
    public int RemoteScore { get; set; }
    public int ExperienceScore { get; set; }
}
