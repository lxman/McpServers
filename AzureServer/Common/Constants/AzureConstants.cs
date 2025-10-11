namespace AzureServer.Common.Constants;

/// <summary>
/// Constants used throughout the Azure MCP server
/// </summary>
public static class AzureConstants
{
    /// <summary>
    /// Default credential target name for Windows Credential Manager
    /// </summary>
    public const string DefaultCredentialTarget = "AzureDevOps";
    
    /// <summary>
    /// Azure DevOps API version
    /// </summary>
    public const string ApiVersion = "7.1";
    
    /// <summary>
    /// Common Azure DevOps work item types
    /// </summary>
    public static class WorkItemTypes
    {
        public const string Bug = "Bug";
        public const string Task = "Task";
        public const string UserStory = "User Story";
        public const string Feature = "Feature";
        public const string Epic = "Epic";
        public const string Issue = "Issue";
        public const string TestCase = "Test Case";
    }
    
    /// <summary>
    /// Common Azure DevOps work item states
    /// </summary>
    public static class WorkItemStates
    {
        public const string New = "New";
        public const string Active = "Active";
        public const string Resolved = "Resolved";
        public const string Closed = "Closed";
        public const string Done = "Done";
        public const string InProgress = "In Progress";
        public const string ToDo = "To Do";
    }
    
    /// <summary>
    /// Common Azure DevOps field names
    /// </summary>
    public static class FieldNames
    {
        public const string Id = "System.Id";
        public const string Title = "System.Title";
        public const string Description = "System.Description";
        public const string State = "System.State";
        public const string WorkItemType = "System.WorkItemType";
        public const string AssignedTo = "System.AssignedTo";
        public const string CreatedBy = "System.CreatedBy";
        public const string CreatedDate = "System.CreatedDate";
        public const string ChangedDate = "System.ChangedDate";
        public const string Tags = "System.Tags";
        public const string TeamProject = "System.TeamProject";
        public const string Priority = "Microsoft.VSTS.Common.Priority";
        public const string AcceptanceCriteria = "Microsoft.VSTS.Common.AcceptanceCriteria";
        public const string StoryPoints = "Microsoft.VSTS.Scheduling.StoryPoints";
        public const string OriginalEstimate = "Microsoft.VSTS.Scheduling.OriginalEstimate";
        public const string RemainingWork = "Microsoft.VSTS.Scheduling.RemainingWork";
        public const string CompletedWork = "Microsoft.VSTS.Scheduling.CompletedWork";
    }
    
    /// <summary>
    /// Azure DevOps URL patterns
    /// </summary>
    public static class UrlPatterns
    {
        public const string Organization = "https://dev.azure.com/{0}";
        public const string Project = "https://dev.azure.com/{0}/{1}";
        public const string WorkItem = "https://dev.azure.com/{0}/{1}/_workitems/edit/{2}";
        public const string Repository = "https://dev.azure.com/{0}/{1}/_git/{2}";
    }
}
