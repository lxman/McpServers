# SeleniumMcp Technical Design

## System Overview

SeleniumMcp is an MCP STDIO server that provides AI-driven job scraping, scoring, and analysis for .NET developer positions across multiple job sites using Selenium/Chromium automation.

## Architecture Layers

```
┌─────────────────────────────────────────────────────────────┐
│ MCP Tools Layer (McpTools/)                                 │
│  • JobScrapingTools, JobStorageTools, AnalysisTools        │
│  • ApplicationTrackingTools, EmailAlertTools                │
│  • Thin wrappers exposing services as MCP tools             │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ Service Orchestration Layer (Services/)                     │
│  • EnhancedJobScrapingService - Main orchestrator          │
│  • JobQueueManager - Async job queue with cancellation     │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ Processing Services Layer (Services/Enhanced/)              │
│  • IntelligentBulkProcessor - Batch processing with paging │
│  • AutomatedSimplifySearch - Multi-search automation       │
│  • NetDeveloperJobScorer - ML-based scoring engine         │
│  • SmartDeduplicationService - Duplicate detection         │
│  • MarketIntelligenceService - Trend analysis              │
│  • ApplicationManagementService - Categorization           │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ Scraper Layer (Services/Scrapers/)                          │
│  • SimplifyJobsScraper, DiceScraper, BuiltInScraper        │
│  • AngelListScraper, StackOverflowScraper, HubSpotScraper  │
│  • GoogleSimplifyJobsService - Google Custom Search API    │
│  • Each uses Selenium WebDriver (ChromeDriver)             │
│  • Semaphore-based concurrency control (1 scraper at time) │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ Data Layer (MongoDB)                                        │
│  • Collection: "search_results" (EnhancedJobListing docs)  │
│  • Collection: "site_configurations" (SiteConfiguration)   │
└─────────────────────────────────────────────────────────────┘
```

## Core Data Flows

### Flow 1: Single Site Scraping (Synchronous)

```
User → scrape_site(site, searchTerm, location)
     → EnhancedJobScrapingService.ScrapeSpecificSiteAsync()
         ├→ Acquires ScrapingSemaphore (timeout: 3 min)
         ├→ Creates scraper via JobSiteScraperFactory
         ├→ Loads SiteConfiguration from MongoDB
         ├→ Scraper.ScrapeJobsAsync() [SELENIUM - 30s to 2min]
         ├→ CalculateMatchScores() (50-year .NET profile)
         ├→ Releases semaphore
         └→ Returns List<EnhancedJobListing> (in memory, NOT saved)

User → save_jobs(jobs, userId) [OPTIONAL - separate call]
     → EnhancedJobScrapingService.SaveJobsAsync()
         ├→ Generate ObjectIds for new jobs
         └→ SaveJobsToDatabase()
             ├→ Check each job URL for duplicates
             ├→ InsertMany new jobs
             └→ ReplaceOne existing jobs (update ScrapedAt, MatchScore)

Duration: 30s - 2min depending on site
Blocks: Yes (synchronous MCP tool call)
```

### Flow 2: Multi-Site Scraping (Synchronous Parallel)

```
User → scrape_multiple_sites(sites[], searchTerm, location)
     → EnhancedJobScrapingService.ScrapeMultipleSitesAsync()
         ├→ Creates Task<> for each site
         ├→ Task.WhenAll() - parallel execution
         │   └→ Each task: ScrapeSpecificSiteAsync() [see Flow 1]
         ├→ Merge all results
         ├→ CalculateMatchScores()
         └→ Returns sorted List<EnhancedJobListing>

Duration: 1-3min (parallelized, limited by semaphore)
Blocks: Yes
Saves: No (caller must use save_jobs)
```

### Flow 3: Bulk Processing (Synchronous with Pagination)

```
User → bulk_process_jobs(searchTerm, location, targetJobs=20)
     → IntelligentBulkProcessor.ProcessJobsBulkAsync()
         ├→ Calculate optimal batch size (5-15 jobs per page)
         ├→ Loop until targetJobs reached:
         │   ├→ Create EnhancedScrapeRequest(maxResults=batchSize)
         │   ├→ SimplifyJobsScraper.ScrapeJobsAsync() [SELENIUM]
         │   ├→ For each job: NetDeveloperJobScorer.CalculateEnhancedMatchScore()
         │   ├→ Accumulate in result.ProcessedJobs List
         │   ├→ Adaptive delay (800-1500ms)
         │   └→ Stop if 3 consecutive batches with score <60
         ├→ Calculate statistics (HighPriorityCount, AverageScore, etc.)
         └→ Returns BulkProcessingResult with ALL jobs in memory

Duration: 2-6min for 20-50 jobs
Blocks: Yes (entire duration)
Saves: No (caller must use save_jobs)
Memory: Holds all jobs until return
```

### Flow 4: Async Job Queue (Background Processing)

```
User → start_bulk_job(searchTerm, location, targetJobs)
     → JobQueueManager.StartJob()
         ├→ Generate jobId (GUID)
         ├→ Create JobState (status: Starting)
         ├→ Create CancellationTokenSource
         ├→ Task.Run(background processor):
         │   └→ IntelligentBulkProcessor.ProcessJobsBulkAsync()
         │       ├→ Progress callback after each batch:
         │       │   └→ JobQueueManager.UpdateProgress()
         │       │       └→ Updates JobState (currentBatch, message, partialResult)
         │       └→ On complete: Updates JobState.Result
         └→ Returns jobId immediately

User → check_job_status(jobId) [Poll every 30-60s]
     → JobQueueManager.GetJobStatus()
         └→ Returns JobStatusResponse:
             ├→ status: "starting|running|completed|failed|cancelled"
             ├→ jobsProcessed, currentBatch, totalBatches
             ├→ elapsedSeconds, progressMessage
             └→ result (when isComplete=true)

User → cancel_job(jobId) [OPTIONAL]
     → JobQueueManager.CancelJob()
         ├→ CancellationTokenSource.Cancel()
         ├→ Thread.Sleep(1000) - grace period
         └→ Returns partial results

Duration: 2-8min (background)
Blocks: No (returns jobId immediately)
⚠️ ISSUE: result contains full List<EnhancedJobListing> (275KB+ on final poll)
```

### Flow 5: Comprehensive Multi-Search

```
User → automated_comprehensive_search(searchTerms[], locations[], targetJobsPerSearch)
     → AutomatedSimplifySearch.RunComprehensiveNetSearchAsync()
         ├→ Nested loops: foreach term, location, experienceLevel
         │   ├→ SimplifyJobsScraper.ScrapeJobsAsync(jobsPerSearch)
         │   ├→ NetDeveloperJobScorer.CalculateEnhancedMatchScore()
         │   ├→ Categorize: HighPriority(80%+), ApplicationReady(60-79%), etc.
         │   ├→ Stop if maxTotalResults reached
         │   └→ Stop if 3 consecutive low-score searches
         └→ Returns EnhancedSearchResults with categorized jobs

Duration: 5-15min depending on combinations
Blocks: Yes
Saves: No
```

## Data Storage Patterns

### Pattern: Scrape → Return → Separate Save

All scraping operations follow this pattern:
1. Scraping tools return `List<EnhancedJobListing>` in memory
2. Jobs are **NOT auto-saved** to MongoDB
3. User must explicitly call `save_jobs(jobs, userId)` to persist
4. `SaveJobsToDatabase()` performs deduplication by URL

**Rationale**: Allows filtering/processing before persistence

### Pattern: Paginated Retrieval

```
User → get_stored_jobs(userId, filters, limit=100, skip=0)
     → EnhancedJobScrapingService.GetStoredJobsAsync()
         ├→ Build MongoDB filter (userId, sites, dates, matchScore, etc.)
         ├→ Query all matching jobs
         ├→ Apply pagination (Skip + Take)
         └→ Returns { totalCount, returnedCount, hasMore, jobs }

Pagination: Client-side (fetch all, then slice)
Default limit: 100 (to prevent token overflow)
```

## Scoring System

### NetDeveloperJobScorer

Profiles a **50-year .NET veteran** with comprehensive experience:

```csharp
Profile = {
    ExperienceYears: 50,
    DesiredSalary: 250000,
    MaxSalary: 350000,
    RemotePreference: Required,
    PreferredRoles: ["Principal Engineer", "Staff Engineer", "Architect", ...],
    RequiredTechnologies: [".NET Core", "C#", "Azure", "SQL Server", ...],
    DesiredBenefits: ["Stock Options", "401k", "Health Insurance", ...]
}
```

**Scoring Components** (0-100 each):
- **TechnologyScore** (30%): Match against 80+ .NET/C# technologies
- **ExperienceScore** (25%): Senior/Principal/Staff level detection
- **SalaryScore** (20%): Alignment with $250K-$350K range
- **RemoteScore** (15%): Remote > Hybrid > On-site
- **CompanyScore** (10%): Company quality indicators

**Final Score**: Weighted average (0-100)

**Thresholds**:
- 90-100: Excellent (immediate apply)
- 75-89: Great (apply)
- 60-74: Good (consider)
- <60: Fair/Skip

## Performance Characteristics

### Selenium Scraping Speeds

| Site | Avg Time | Rate Limits | Notes |
|------|----------|-------------|-------|
| SimplifyJobs | 5-8s/job | Moderate | Primary scraper |
| Dice | 10-15s/job | Aggressive | Needs careful rate limiting |
| BuiltIn | 8-12s/job | Moderate | Stable |
| StackOverflow | 12-20s/job | Light | Slow but reliable |
| HubSpot | 15-25s/job | Moderate | Complex pages |

### Scraping Semaphore

**Critical**: Only **1 Selenium operation at a time**
- Defined: `EnhancedJobScrapingService` line 21
- Timeout: 3 minutes
- Purpose: Prevent ChromeDriver conflicts, reduce detection risk

### Bulk Processing Performance

| Target Jobs | Batches | Duration | Memory |
|-------------|---------|----------|--------|
| 10 jobs | 2 batches | 1-2 min | ~50KB |
| 20 jobs | 3 batches | 2-4 min | ~100KB |
| 50 jobs | 5 batches | 5-8 min | ~250KB |
| 100 jobs | 7 batches | 8-15 min | ~500KB |

**Adaptive Batch Sizing**:
```csharp
targetJobs <= 10  → batchSize = 5
targetJobs <= 20  → batchSize = 8
targetJobs <= 50  → batchSize = 10
targetJobs > 50   → batchSize = 15
```

## Key Design Decisions

### 1. Synchronous Tool Calls
**Decision**: Most tools block until completion
**Rationale**: Simpler for AI to use, MCP protocol is request/response
**Trade-off**: Long operations (2-6min) can timeout or use excessive tokens

### 2. Separate Save Operation
**Decision**: Scraping returns jobs in memory, saving is explicit
**Rationale**: Allows filtering, deduplication, scoring before persistence
**Benefit**: User controls what gets saved

### 3. Semaphore-Based Concurrency
**Decision**: Only 1 Selenium operation at a time
**Rationale**: ChromeDriver stability, reduce bot detection
**Trade-off**: Parallelism limited, but safer

### 4. No Auto-Incremental Saving
**Decision**: Bulk operations accumulate ALL jobs before returning
**Rationale**: Consistent with synchronous pattern, simpler API
**Issue**: Memory growth, large payloads on return

### 5. Score-Based Early Stopping
**Decision**: Stop if 3 consecutive batches have no jobs scoring >60%
**Rationale**: Don't waste time on low-quality results
**Benefit**: Faster completion on poor searches

## MongoDB Schema

### Collection: "search_results"

```javascript
{
  _id: ObjectId,
  userId: string,              // Owner of this job listing
  jobId: string,               // External job ID (for deduplication)
  jobUrl: string,              // Unique URL (dedup key)
  title: string,
  company: string,
  location: string,
  salary: string,
  summary: string,
  description: string,         // Short description
  fullDescription: string,     // Complete job description
  url: string,
  datePosted: DateTime,
  scrapedAt: DateTime,
  sourceSite: int,             // JobSite enum (2=Dice, 11=SimplifyJobs, etc.)
  matchScore: double,          // 0-100 calculated score
  isRemote: bool,
  isApplied: bool,
  requiredSkills: string[],
  notes: string                // Scoring breakdown, metadata
}
```

**Indexes**:
- `{ userId: 1, scrapedAt: -1 }`
- `{ jobUrl: 1 }` (unique)
- `{ matchScore: -1 }`

### Collection: "site_configurations"

```javascript
{
  _id: ObjectId,
  siteName: string,            // JobSite enum name
  isActive: bool,
  lastUpdated: DateTime,
  selectors: {                 // CSS selectors for scraping
    jobCard: string,
    title: string,
    company: string,
    location: string,
    // ... site-specific
  }
}
```

## Known Issues

### Issue 1: Token Explosion in Async Jobs
**Symptom**: AI runs out of tokens before seeing final results
**Cause**: `check_job_status()` returns full `List<EnhancedJobListing>` on every poll
**Impact**:
- 78 polls × 150KB avg = 11.7MB redundant data
- ~3M tokens wasted on polling
- Conversation terminated before results processed

**Location**:
- `JobQueueManager.cs:121-145` - Creates full `partialResult` copy
- `JobStatusResponse` - Includes `Result` and `PartialResult` fields

**Proposed Fix**: Return summary statistics during polling, full results only on explicit request

### Issue 2: Synchronous Operations Block for Minutes
**Symptom**: Long-running tools timeout or hang AI conversations
**Cause**: `bulk_process_jobs`, `automated_comprehensive_search` are synchronous
**Impact**: Poor UX, AI must wait 5-15 minutes
**Mitigation**: Use async job queue pattern (`start_bulk_job`)

### Issue 3: No Incremental Saving During Bulk Operations
**Symptom**: If process crashes, all work is lost
**Cause**: Jobs only saved after complete return
**Impact**: Brittle for long operations
**Future**: Consider incremental saves during processing

## Dependencies

- **Selenium WebDriver**: Chromium automation
- **MongoDB.Driver**: Data persistence
- **ModelContextProtocol.Server**: MCP STDIO server framework
- **Serilog**: Structured logging
- **System.Text.Json**: Serialization

## Configuration

**appsettings.json**:
```json
{
  "MongoDbSettings": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "job_search"
  }
}
```

## Tool Categories

### Scraping Tools (JobScrapingTools.cs)
- `scrape_site` - Single site scraping
- `scrape_multiple_sites` - Parallel multi-site
- `simplify_jobs_google` - Google Custom Search discovery

### Storage Tools (JobStorageTools.cs)
- `save_jobs` - Persist jobs to MongoDB
- `get_stored_jobs` - Retrieve with filtering/pagination
- `get_screenshot` - Capture job page screenshot

### Analysis Tools (AnalysisTools.cs)
- `bulk_process_jobs` - Synchronous batch processing
- `simplify_jobs_enhanced` - Enhanced SimplifyJobs processing
- `automated_comprehensive_search` - Multi-term/location search
- `smart_deduplication` - Duplicate detection and merging
- `categorize_applications` - Job categorization
- `market_intelligence` - Trend and salary analysis
- `start_bulk_job` - Async job initiation
- `check_job_status` - Async job status polling
- `cancel_job` - Async job cancellation
- `list_bulk_jobs` - List all async jobs

### Application Tracking Tools (ApplicationTrackingTools.cs)
- `track_application` - Record job applications
- `update_application_status` - Update application state

### Email Alert Tools (EmailAlertTools.cs)
- `get_email_alert_jobs` - Parse job alerts from email
- `get_email_alert_summary` - Summarize email alerts
- `get_enhanced_email_alerts` - Enhanced email processing

## Testing Patterns

All tools return JSON with consistent structure:
```json
{
  "success": true|false,
  "error": "error message" // if success=false
  // ... tool-specific fields
}
```

**Error Handling**: Try/catch in all MCP tools, log errors, return `success: false`

---

**Last Updated**: 2025-11-09
**Version**: 1.0
**Maintainer**: SeleniumMcp Development Team
