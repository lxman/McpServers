# AzureMcp - Azure DevOps MCP Server

An MCP (Model Context Protocol) server that provides Azure DevOps integration, allowing Claude to interact with Azure DevOps projects, work items, and repositories.

## Features

### Azure DevOps Integration
- **Projects**: List and retrieve Azure DevOps projects
- **Work Items**: Get, query, and create work items (bugs, tasks, user stories)
- **Repositories**: List and retrieve Git repositories
- **Authentication**: Secure PAT-based authentication with Windows Credential Manager support

## Prerequisites

1. **Azure DevOps Account**: Access to an Azure DevOps organization
2. **Personal Access Token (PAT)**: Created in Azure DevOps with appropriate permissions
3. **.NET 9.0**: Required for running the MCP server
4. **Windows Credential Manager**: For secure PAT storage (recommended)

## Setup

### 1. Create Personal Access Token

1. Go to Azure DevOps → User Settings → Personal Access Tokens
2. Create a new token with these scopes:
   - **Work Items**: Read & Write
   - **Code**: Read (for repositories)
   - **Project and Team**: Read

### 2. Store PAT in Windows Credential Manager

**Option A: Using GUI**
1. Open Windows Credential Manager
2. Add Generic Credential:
   - **Internet or network address**: `AzureDevOps`
   - **User name**: Your email
   - **Password**: Your PAT token

**Option B: Using Code**
```csharp
using CredentialManagement;

var cred = new Credential
{
    Target = "AzureDevOps",
    Username = "your-email@company.com",
    Password = "your-pat-token",
    Type = CredentialType.Generic,
    PersistanceType = PersistanceType.LocalComputer
};
cred.Save();
```

### 3. Configure Application

Update `appsettings.json`:
```json
{
  "AzureDevOps": {
    "OrganizationUrl": "https://dev.azure.com/yourorganization",
    "CredentialTarget": "AzureDevOps",
    "DefaultProject": "YourDefaultProject"
  }
}
```

### 4. Build and Run

```bash
dotnet build AzureMcp/AzureMcp.csproj
dotnet run --project AzureMcp/AzureMcp.csproj
```

## Available MCP Tools

### Project Operations
- `devops_list_projects`: List all accessible projects
- `devops_get_project`: Get details of a specific project

### Work Item Operations
- `devops_get_work_item`: Retrieve a work item by ID
- `devops_get_work_items`: Query work items from a project (supports WIQL)
- `devops_create_work_item`: Create new work items (bugs, tasks, user stories)

### Repository Operations
- `devops_list_repositories`: List repositories in a project
- `devops_get_repository`: Get details of a specific repository

## Authentication Methods

The server attempts authentication in this order:

1. **Windows Credential Manager** (recommended)
2. **Configuration file** (`appsettings.json`)
3. **Environment variable** (`AZURE_DEVOPS_PAT`)

## Usage Examples

### List Projects
```
Use the devops_list_projects tool to see all available projects.
```

### Query Work Items
```
Use devops_get_work_items with project name "MyProject" and optional WIQL query:
"SELECT [System.Id], [System.Title] FROM WorkItems WHERE [System.State] = 'Active'"
```

### Create Work Item
```
Use devops_create_work_item to create a new bug:
- Project: "MyProject"
- Type: "Bug"
- Title: "Fix login issue"
- Description: "Users cannot log in with special characters"
- Priority: 1
```

## Error Handling

The server provides detailed error messages for common issues:
- Invalid PAT tokens
- Missing permissions
- Project/work item not found
- Network connectivity issues

## Security

- PAT tokens are stored encrypted in Windows Credential Manager
- No credentials are logged or exposed in error messages
- All Azure DevOps communication uses HTTPS

## Troubleshooting

### Authentication Issues
1. Verify PAT token has correct permissions
2. Check organization URL format: `https://dev.azure.com/yourorg`
3. Ensure PAT token hasn't expired

### Connection Issues
1. Verify internet connectivity
2. Check if your organization uses different Azure DevOps URL
3. Confirm project names are correct

### Build Issues
- CredentialManagement package warnings are expected on .NET 9
- Ensure all required NuGet packages are restored

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test thoroughly
5. Submit a pull request

## License

This project is part of the McpServers solution.
