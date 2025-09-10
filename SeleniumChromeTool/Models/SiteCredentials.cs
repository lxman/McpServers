namespace SeleniumChromeTool.Models;

public class SiteCredentials
{
    public JobSite Site { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; } = true;
    public DateTime? LastLoginAttempt { get; set; }
    public bool IsSessionValid { get; set; }
    public string? SessionCookies { get; set; }
}

public class AuthenticatedScrapeRequest : EnhancedScrapeRequest
{
    public List<SiteCredentials> SiteCredentials { get; set; } = [];
    public bool UseExistingSessions { get; set; } = true;
    public bool SaveSessionCookies { get; set; } = true;
    public int LoginTimeoutSeconds { get; set; } = 30;
}

public class LoginResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public JobSite Site { get; set; }
    public bool SessionSaved { get; set; }
    public string? ErrorDetails { get; set; }
}
