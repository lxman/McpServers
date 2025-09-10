﻿namespace SeleniumChromeTool.Models;

public class RateLimitConfig
{
    public int RequestsPerMinute { get; set; } = 10;
    public int DelayBetweenRequests { get; set; } = 3000;
    public int RetryAttempts { get; set; } = 3;
    public int RetryDelay { get; set; } = 5000;
}