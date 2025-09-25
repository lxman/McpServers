using LibGit2Sharp;

namespace McpCodeEditor.Services;

public class GitCommitInfo
{
    public string Sha { get; set; } = string.Empty;
    public string ShortSha { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public List<string> Parents { get; set; } = [];
}

public class GitFileStatus
{
    public string FilePath { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsStaged { get; set; }
    public bool IsModified { get; set; }
    public bool IsNew { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsRenamed { get; set; }
}

public class GitBranchInfo
{
    public string Name { get; set; } = string.Empty;
    public string FriendlyName { get; set; } = string.Empty;
    public bool IsCurrentBranch { get; set; }
    public bool IsRemote { get; set; }
    public string RemoteName { get; set; } = string.Empty;
    public string UpstreamBranch { get; set; } = string.Empty;
    public int AheadBy { get; set; }
    public int BehindBy { get; set; }
}

public class GitBlameInfo
{
    public int LineNumber { get; set; }
    public string Content { get; set; } = string.Empty;
    public string CommitSha { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string CommitMessage { get; set; } = string.Empty;
}

public class GitService(CodeEditorConfigurationService config)
{
    /// <summary>
    /// Get the status of the working directory
    /// </summary>
    public async Task<object> GetStatusAsync(string? repositoryPath = null)
    {
        try
        {
            var repoPath = GetRepositoryPath(repositoryPath);

            if (!IsGitRepository(repoPath))
            {
                return new { success = false, error = "Not a git repository", path = repoPath };
            }

            using var repo = new Repository(repoPath);
            var status = repo.RetrieveStatus();

            var result = new
            {
                success = true,
                repository_path = repoPath,
                current_branch = repo.Head.FriendlyName,
                is_dirty = status.IsDirty,
                ahead_by = repo.Head.TrackingDetails?.AheadBy ?? 0,
                behind_by = repo.Head.TrackingDetails?.BehindBy ?? 0,
                files = status.Select(entry => new GitFileStatus
                {
                    FilePath = entry.FilePath,
                    Status = entry.State.ToString(),
                    IsStaged = (entry.State & FileStatus.NewInIndex) != 0 ||
                              (entry.State & FileStatus.ModifiedInIndex) != 0 ||
                              (entry.State & FileStatus.DeletedFromIndex) != 0,
                    IsModified = (entry.State & FileStatus.ModifiedInWorkdir) != 0,
                    IsNew = (entry.State & FileStatus.NewInWorkdir) != 0,
                    IsDeleted = (entry.State & FileStatus.DeletedFromWorkdir) != 0,
                    IsRenamed = (entry.State & FileStatus.RenamedInIndex) != 0
                }).ToArray(),
                summary = new
                {
                    total_files = status.Count(),
                    staged_files = status.Staged.Count(),
                    modified_files = status.Modified.Count(),
                    added_files = status.Added.Count(),
                    removed_files = status.Removed.Count(),
                    untracked_files = status.Untracked.Count()
                }
            };

            return result;
        }
        catch (Exception ex)
        {
            return new { success = false, error = ex.Message };
        }
    }

    /// <summary>
    /// Get diff between commits or working directory
    /// </summary>
    public async Task<object> GetDiffAsync(string? repositoryPath = null, string? fromCommit = null,
        string? toCommit = null, string? filePath = null, int contextLines = 3)
    {
        try
        {
            var repoPath = GetRepositoryPath(repositoryPath);

            if (!IsGitRepository(repoPath))
            {
                return new { success = false, error = "Not a git repository", path = repoPath };
            }

            using var repo = new Repository(repoPath);
            Patch? patch = null;

            if (string.IsNullOrEmpty(fromCommit) && string.IsNullOrEmpty(toCommit))
            {
                // Diff working directory against HEAD
                patch = repo.Diff.Compare<Patch>(repo.Head.Tip?.Tree, DiffTargets.Index | DiffTargets.WorkingDirectory);
            }
            else if (!string.IsNullOrEmpty(fromCommit) && string.IsNullOrEmpty(toCommit))
            {
                // Diff from specific commit to working directory
                var commit = repo.Lookup<Commit>(fromCommit);
                if (commit == null)
                {
                    return new { success = false, error = $"Commit not found: {fromCommit}" };
                }
                patch = repo.Diff.Compare<Patch>(commit.Tree, DiffTargets.Index | DiffTargets.WorkingDirectory);
            }
            else if (!string.IsNullOrEmpty(fromCommit) && !string.IsNullOrEmpty(toCommit))
            {
                // Diff between two commits
                var fromCommitObj = repo.Lookup<Commit>(fromCommit);
                var toCommitObj = repo.Lookup<Commit>(toCommit);

                if (fromCommitObj == null)
                {
                    return new { success = false, error = $"From commit not found: {fromCommit}" };
                }
                if (toCommitObj == null)
                {
                    return new { success = false, error = $"To commit not found: {toCommit}" };
                }

                patch = repo.Diff.Compare<Patch>(fromCommitObj.Tree, toCommitObj.Tree);
            }

            if (patch == null)
            {
                return new { success = false, error = "Could not generate diff" };
            }

            // Filter by file path if specified
            List<PatchEntryChanges> patchEntries = patch.Where(entry =>
                string.IsNullOrEmpty(filePath) ||
                entry.Path.Contains(filePath, StringComparison.OrdinalIgnoreCase)).ToList();

            var result = new
            {
                success = true,
                repository_path = repoPath,
                from_commit = fromCommit ?? "HEAD",
                to_commit = toCommit ?? "Working Directory",
                file_filter = filePath,
                stats = new
                {
                    total_files = patchEntries.Count,
                    additions = patchEntries.Sum(e => e.LinesAdded),
                    deletions = patchEntries.Sum(e => e.LinesDeleted)
                },
                files = patchEntries.Select(entry => new
                {
                    path = entry.Path,
                    old_path = entry.OldPath,
                    status = entry.Status.ToString(),
                    lines_added = entry.LinesAdded,
                    lines_deleted = entry.LinesDeleted,
                    patch = entry.Patch
                }).ToArray()
            };

            return result;
        }
        catch (Exception ex)
        {
            return new { success = false, error = ex.Message };
        }
    }

    /// <summary>
    /// Get commit history (log)
    /// </summary>
    public async Task<object> GetLogAsync(string? repositoryPath = null, int maxCount = 20,
        string? filePath = null, string? branch = null)
    {
        try
        {
            var repoPath = GetRepositoryPath(repositoryPath);

            if (!IsGitRepository(repoPath))
            {
                return new { success = false, error = "Not a git repository", path = repoPath };
            }

            using var repo = new Repository(repoPath);

            // Determine starting point
            var commitFilter = new CommitFilter();

            if (!string.IsNullOrEmpty(branch))
            {
                var targetBranch = repo.Branches[branch];
                if (targetBranch == null)
                {
                    return new { success = false, error = $"Branch not found: {branch}" };
                }
                commitFilter.IncludeReachableFrom = targetBranch;
            }
            else
            {
                commitFilter.IncludeReachableFrom = repo.Head;
            }

            // Filter by file path if specified
            if (!string.IsNullOrEmpty(filePath))
            {
                commitFilter.SortBy = CommitSortStrategies.Topological;
            }

            IEnumerable<Commit> commits = repo.Commits.QueryBy(commitFilter).Take(maxCount);

            // If filtering by file path, filter commits that touched the file
            if (!string.IsNullOrEmpty(filePath))
            {
                commits = commits.Where(commit =>
                {
                    if (commit.Parents.Count() == 0) return true; // Initial commit

                    var parentCommit = commit.Parents.First();
                    var patch = repo.Diff.Compare<Patch>(parentCommit.Tree, commit.Tree);
                    return patch.Any(entry => entry.Path.Contains(filePath, StringComparison.OrdinalIgnoreCase));
                });
            }

            var result = new
            {
                success = true,
                repository_path = repoPath,
                branch = branch ?? repo.Head.FriendlyName,
                file_filter = filePath,
                max_count = maxCount,
                commits = commits.Select(commit => new GitCommitInfo
                {
                    Sha = commit.Sha,
                    ShortSha = commit.Sha[..8],
                    Message = commit.MessageShort,
                    Author = commit.Author.Name,
                    Email = commit.Author.Email,
                    Date = commit.Author.When.DateTime,
                    Parents = commit.Parents.Select(p => p.Sha[..8]).ToList()
                }).ToArray()
            };

            return result;
        }
        catch (Exception ex)
        {
            return new { success = false, error = ex.Message };
        }
    }

    /// <summary>
    /// Get blame information for a file
    /// </summary>
    public async Task<object> GetBlameAsync(string filePath, string? repositoryPath = null)
    {
        try
        {
            var repoPath = GetRepositoryPath(repositoryPath);

            if (!IsGitRepository(repoPath))
            {
                return new { success = false, error = "Not a git repository", path = repoPath };
            }

            // Make file path relative to repository root
            var relativePath = Path.GetRelativePath(repoPath, Path.GetFullPath(filePath));

            using var repo = new Repository(repoPath);

            if (!File.Exists(Path.Combine(repoPath, relativePath)))
            {
                return new { success = false, error = $"File not found: {relativePath}" };
            }

            var blame = repo.Blame(relativePath);
            var fileLines = await File.ReadAllLinesAsync(Path.Combine(repoPath, relativePath));

            var result = new
            {
                success = true,
                repository_path = repoPath,
                file_path = relativePath,
                total_lines = fileLines.Length,
                blame_info = blame.Select((hunk, index) => new GitBlameInfo
                {
                    LineNumber = index + 1,
                    Content = index < fileLines.Length ? fileLines[index] : "",
                    CommitSha = hunk.FinalCommit.Sha[..8],
                    Author = hunk.FinalCommit.Author.Name,
                    Email = hunk.FinalCommit.Author.Email,
                    Date = hunk.FinalCommit.Author.When.DateTime,
                    CommitMessage = hunk.FinalCommit.MessageShort
                }).ToArray()
            };

            return result;
        }
        catch (Exception ex)
        {
            return new { success = false, error = ex.Message };
        }
    }

    /// <summary>
    /// List branches
    /// </summary>
    public async Task<object> GetBranchesAsync(string? repositoryPath = null, bool includeRemote = true)
    {
        try
        {
            var repoPath = GetRepositoryPath(repositoryPath);

            if (!IsGitRepository(repoPath))
            {
                return new { success = false, error = "Not a git repository", path = repoPath };
            }

            using var repo = new Repository(repoPath);

            var branches = repo.Branches
                .Where(branch => includeRemote || !branch.IsRemote)
                .Select(branch => new GitBranchInfo
                {
                    Name = branch.CanonicalName,
                    FriendlyName = branch.FriendlyName,
                    IsCurrentBranch = branch.IsCurrentRepositoryHead,
                    IsRemote = branch.IsRemote,
                    RemoteName = branch.RemoteName ?? "",
                    UpstreamBranch = branch.TrackedBranch?.FriendlyName ?? "",
                    AheadBy = branch.TrackingDetails?.AheadBy ?? 0,
                    BehindBy = branch.TrackingDetails?.BehindBy ?? 0
                }).ToArray();

            var result = new
            {
                success = true,
                repository_path = repoPath,
                current_branch = repo.Head.FriendlyName,
                include_remote = includeRemote,
                branches = branches,
                summary = new
                {
                    total_branches = branches.Length,
                    local_branches = branches.Count(b => !b.IsRemote),
                    remote_branches = branches.Count(b => b.IsRemote),
                    current_branch = branches.FirstOrDefault(b => b.IsCurrentBranch)?.FriendlyName ?? "Unknown"
                }
            };

            return result;
        }
        catch (Exception ex)
        {
            return new { success = false, error = ex.Message };
        }
    }

    private string GetRepositoryPath(string? repositoryPath)
    {
        if (!string.IsNullOrEmpty(repositoryPath))
        {
            return Path.GetFullPath(repositoryPath);
        }

        // Use current workspace
        return config.DefaultWorkspace;
    }

    private static bool IsGitRepository(string path)
    {
        try
        {
            using var repo = new Repository(path);
            return true;
        }
        catch
        {
            // Try to find .git directory in parent directories
            var current = new DirectoryInfo(path);
            while (current != null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, ".git")))
                {
                    return true;
                }
                current = current.Parent;
            }
            return false;
        }
    }
}
