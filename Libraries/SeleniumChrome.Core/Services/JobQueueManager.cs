using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using SeleniumChrome.Core.Models;
using SeleniumChrome.Core.Services.Enhanced;

namespace SeleniumChrome.Core.Services;

/// <summary>
/// Manages long-running job queue with progress tracking and cancellation
/// </summary>
public class JobQueueManager
{
    private readonly ILogger<JobQueueManager> _logger;
    private readonly ConcurrentDictionary<string, JobState> _jobs = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokens = new();
    private const int MaxCompletedJobsRetention = 50; // Keep last 50 completed jobs

    public JobQueueManager(ILogger<JobQueueManager> logger)
    {
        _logger = logger;
        
        // Start cleanup task for old completed jobs
        _ = Task.Run(async () => await CleanupOldJobsAsync());
    }

    /// <summary>
    /// Start a new bulk processing job
    /// </summary>
    public string StartJob(BulkProcessingRequest request, Func<string, BulkProcessingRequest, CancellationToken, Task<BulkProcessingResult>> processor)
    {
        var jobId = Guid.NewGuid().ToString();
        var cts = new CancellationTokenSource();
        
        var jobState = new JobState
        {
            JobId = jobId,
            Request = request,
            Status = JobStatus.Starting,
            StartTime = DateTime.UtcNow,
            ProgressMessage = "Initializing job..."
        };

        _jobs[jobId] = jobState;
        _cancellationTokens[jobId] = cts;

        // Start background processing
        _ = Task.Run(async () =>
        {
            try
            {
                _logger.LogInformation($"Starting background job {jobId} for {request.SearchTerm} in {request.Location}");
                
                jobState.Status = JobStatus.Running;
                jobState.ProgressMessage = "Processing jobs...";
                
                BulkProcessingResult result = await processor(jobId, request, cts.Token);
                
                jobState.Status = JobStatus.Completed;
                jobState.Result = result;
                jobState.EndTime = DateTime.UtcNow;
                jobState.ProgressMessage = $"Completed: {result.ProcessedJobs.Count} jobs processed";
                
                _logger.LogInformation($"Job {jobId} completed successfully");
            }
            catch (OperationCanceledException)
            {
                jobState.Status = JobStatus.Cancelled;
                jobState.EndTime = DateTime.UtcNow;
                jobState.ProgressMessage = $"Cancelled: {jobState.Result?.ProcessedJobs.Count ?? 0} jobs processed before cancellation";
                
                _logger.LogInformation($"Job {jobId} was cancelled");
            }
            catch (Exception ex)
            {
                jobState.Status = JobStatus.Failed;
                jobState.EndTime = DateTime.UtcNow;
                jobState.ErrorMessage = ex.Message;
                jobState.ProgressMessage = $"Failed: {ex.Message}";
                
                _logger.LogError(ex, $"Job {jobId} failed");
            }
            finally
            {
                // Cleanup cancellation token
                if (_cancellationTokens.TryRemove(jobId, out CancellationTokenSource? token))
                {
                    token.Dispose();
                }
            }
        }, cts.Token);

        return jobId;
    }

    /// <summary>
    /// Get current status of a job
    /// </summary>
    public JobStatusResponse GetJobStatus(string jobId)
    {
        if (!_jobs.TryGetValue(jobId, out JobState? jobState))
        {
            return new JobStatusResponse
            {
                Found = false,
                Message = $"Job {jobId} not found"
            };
        }

        double elapsedSeconds = jobState.EndTime.HasValue
            ? (jobState.EndTime.Value - jobState.StartTime).TotalSeconds
            : (DateTime.UtcNow - jobState.StartTime).TotalSeconds;

        return new JobStatusResponse
        {
            Found = true,
            JobId = jobId,
            Status = jobState.Status.ToString(),
            ProgressMessage = jobState.ProgressMessage,
            SearchTerm = jobState.Request.SearchTerm,
            Location = jobState.Request.Location,
            JobsProcessed = jobState.Summary?.JobsProcessed ?? jobState.Result?.ProcessedJobs.Count ?? 0,
            CurrentBatch = jobState.CurrentBatch,
            TotalBatches = jobState.TotalBatches,
            ElapsedSeconds = (int)elapsedSeconds,
            IsComplete = jobState.Status is JobStatus.Completed or JobStatus.Failed or JobStatus.Cancelled,
            Summary = jobState.Summary,
            ErrorMessage = jobState.ErrorMessage
        };
    }

    /// <summary>
    /// Cancel a running job and return partial results
    /// </summary>
    public JobCancellationResponse CancelJob(string jobId)
    {
        if (!_jobs.TryGetValue(jobId, out JobState? jobState))
        {
            return new JobCancellationResponse
            {
                Success = false,
                Message = $"Job {jobId} not found"
            };
        }

        if (jobState.Status is JobStatus.Completed or JobStatus.Failed or JobStatus.Cancelled)
        {
            return new JobCancellationResponse
            {
                Success = false,
                Message = $"Job {jobId} is already {jobState.Status.ToString().ToLower()}",
                Status = jobState.Status.ToString(),
                PartialResults = jobState.Result
            };
        }

        // Request cancellation
        if (_cancellationTokens.TryGetValue(jobId, out CancellationTokenSource? cts))
        {
            _logger.LogInformation($"Requesting cancellation for job {jobId}");
            cts.Cancel();
        }

        // Wait briefly for graceful shutdown
        Thread.Sleep(1000);

        return new JobCancellationResponse
        {
            Success = true,
            Message = $"Job {jobId} cancellation requested. {jobState.Result?.ProcessedJobs.Count ?? 0} jobs processed before cancellation.",
            Status = jobState.Status.ToString(),
            PartialResults = jobState.Result,
            JobsProcessed = jobState.Result?.ProcessedJobs.Count ?? 0
        };
    }

    /// <summary>
    /// Get the full results of a completed job
    /// </summary>
    public BulkProcessingResult? GetJobResults(string jobId)
    {
        if (!_jobs.TryGetValue(jobId, out JobState? jobState))
        {
            return null;
        }

        return jobState.Result;
    }

    /// <summary>
    /// List all jobs (active and recent completed)
    /// </summary>
    public List<JobSummary> ListJobs(bool includeCompleted = true)
    {
        List<JobSummary> summaries = (from kvp in _jobs
        select kvp.Value
        into jobState
        where includeCompleted || jobState.Status is not (JobStatus.Completed or JobStatus.Failed or JobStatus.Cancelled)
        select new JobSummary
        {
            JobId = jobState.JobId,
            Status = jobState.Status.ToString(),
            SearchTerm = jobState.Request.SearchTerm,
            Location = jobState.Request.Location,
            StartTime = jobState.StartTime,
            EndTime = jobState.EndTime,
            JobsProcessed = jobState.Result?.ProcessedJobs.Count ?? 0
        }).ToList();

        return summaries.OrderByDescending(s => s.StartTime).ToList();
    }

    /// <summary>
    /// Update progress for a job (called by processor)
    /// Uses lightweight summary to avoid token explosion
    /// </summary>
    public void UpdateProgress(string jobId, int currentBatch, int totalBatches, string message, BulkProcessingSummary? summary = null)
    {
        if (!_jobs.TryGetValue(jobId, out JobState? jobState)) return;
        jobState.CurrentBatch = currentBatch;
        jobState.TotalBatches = totalBatches;
        jobState.ProgressMessage = message;

        if (summary != null)
        {
            jobState.Summary = summary;
        }
    }

    /// <summary>
    /// Cleanup completed jobs older than 1 hour
    /// </summary>
    private async Task CleanupOldJobsAsync()
    {
        while (true)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(10));

                DateTime cutoffTime = DateTime.UtcNow.AddHours(-1);
                List<KeyValuePair<string, JobState>> completedJobs = _jobs
                    .Where(kvp => kvp.Value.Status is JobStatus.Completed or JobStatus.Failed or JobStatus.Cancelled)
                    .Where(kvp => kvp.Value.EndTime.HasValue && kvp.Value.EndTime.Value < cutoffTime)
                    .ToList();

                // Keep at least MaxCompletedJobsRetention
                if (completedJobs.Count <= MaxCompletedJobsRetention) continue;
                IEnumerable<KeyValuePair<string, JobState>> toRemove = completedJobs.OrderBy(kvp => kvp.Value.EndTime).Take(completedJobs.Count - MaxCompletedJobsRetention);
                
                foreach (KeyValuePair<string, JobState> kvp in toRemove)
                {
                    _jobs.TryRemove(kvp.Key, out _);
                    _logger.LogDebug($"Cleaned up old job {kvp.Key}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in job cleanup task");
            }
        }
    }
}