
# AzureMcp - Comprehensive Azure MCP Server

An MCP (Model Context Protocol) server that provides seamless Azure integration, allowing Claude to interact with Azure DevOps, Azure Storage, Resource Management, and Cost Management with zero configuration.


## 🚀 Zero Configuration Experience

**Just point Claude to the executable and go!** No complex setup required.

## ✨ Features

### Azure DevOps Integration
- **Projects**: List and retrieve Azure DevOps projects
- **Work Items**: Get, query, and create work items (bugs, tasks, user stories)
- **Repositories**: List and retrieve Git repositories
- **Build Pipelines**: Access build definitions, build history, and trigger builds
- **YAML Files**: Retrieve and edit pipeline YAML files directly
- **Release Pipelines**: Access release definitions and deployment history


### Azure Storage Integration
- **Storage Accounts**: List and retrieve storage accounts across subscriptions
- **Blob Containers**: Create, list, and manage blob containers
- **Blob Operations**: Upload, download, copy, and delete blobs
- **Blob Metadata**: Get and set custom metadata on blobs
- **SAS Tokens**: Generate temporary access URLs for secure sharing

### Authentication
- **Local Configuration**: Simple JSON file in executable directory
- **Secure Storage**: No credentials in system-wide locations
- **Zero Setup**: Works immediately after credential configuration

## 📋 Quick Start

### 1. Build the Server
```bash
cd path/to/AzureMcp
dotnet publish -c Release
```

### 2. Configure Credentials
Create `devops-config.json` in the executable directory:

```json
{
  "AzureDevOps": {
    "OrganizationUrl": "https://dev.azure.com/yourorganization",
    "PersonalAccessToken": "your-pat-token-here"
  }
}
```

### 3. Add to Claude Desktop Config
Edit your `claude_desktop_config.json`:
```json
{
  "mcpServers": {
    "azure": {
      "command": "path/to/AzureMcp/bin/Release/net9.0/AzureMcp.exe"
    }
  }
}
```

### 4. Restart Claude Desktop
**That's it!** Claude can now access your Azure DevOps environment.

## 🔐 PAT Setup

### Create Personal Access Token

1. Go to **Azure DevOps** → **User Settings** → **Personal Access Tokens**
2. Create a new token with these scopes:
    - **Work Items**: Read & Write
    - **Code**: Read & Write (for repositories and YAML files)
    - **Build**: Read & Execute (for pipeline access)
    - **Release**: Read (for release pipeline access)
    - **Project and Team**: Read

### Configuration File Location

The `devops-config.json` file must be in the **same directory as the executable**:
```
AzureMcp/bin/Release/net9.0/
├── AzureMcp.exe
├── devops-config.json  ← Must be here
└── [other DLLs...]
```

## 🔧 Available Tools

### Project Operations
- `list_projects`: List all accessible projects
- `get_project`: Get details of a specific project

### Work Item Operations
- `get_work_item`: Retrieve a work item by ID
- `get_work_items`: Query work items from a project (supports WIQL)
- `create_work_item`: Create new work items (bugs, tasks, user stories)

### Repository Operations
- `list_repositories`: List repositories in a project
- `get_repository`: Get details of a specific repository

### Pipeline Operations
- `list_build_definitions`: List all build pipelines in a project
- `get_build_definition`: Get details of a specific pipeline
- `list_builds`: Get build history and recent builds
- `get_build`: Get details of a specific build
- `queue_build`: Trigger a pipeline build

### YAML File Operations
- `get_repository_file`: Get content of any repository file (including YAML)
- `update_repository_file`: Update repository files with commit messages
- `find_yaml_pipeline_files`: Discover YAML pipeline files in repositories
- `get_pipeline_yaml`: Get YAML content for a specific pipeline
- `update_pipeline_yaml`: Update pipeline YAML files directly

### Release Pipeline Operations
- `list_release_definitions`: List release pipelines
- `get_release_definition`: Get release pipeline details
- `get_releases`: Get releases for a project (optionally filtered by definition)



### Azure Storage Operations

#### Storage Account Management
- `list_storage_accounts`: List all storage accounts (optionally filtered by subscription)
- `get_storage_account`: Get details of a specific storage account

#### Container Management
- `list_containers`: List all containers in a storage account
- `get_container`: Get details of a specific container
- `create_container`: Create a new container with optional metadata
- `delete_container`: Delete a container
- `container_exists`: Check if a container exists

#### Blob Operations
- `list_blobs`: List blobs in a container (with optional prefix filter)
- `get_blob_properties`: Get properties and metadata of a blob
- `download_blob_as_text`: Download blob content as text
- `upload_blob_from_text`: Upload text content as a blob
- `delete_blob`: Delete a blob
- `blob_exists`: Check if a blob exists
- `copy_blob`: Copy a blob between containers or accounts

#### Blob Metadata
- `get_blob_metadata`: Get custom metadata for a blob
- `set_blob_metadata`: Set custom metadata on a blob

#### SAS (Shared Access Signature) Tokens
- `generate_blob_sas_url`: Generate temporary access URL for a blob
- `generate_container_sas_url`: Generate temporary access URL for a container

## 💬 Usage Examples

### Pipeline Management
```
"Show me all build pipelines in the TADERATCS project"
"Get the latest builds for the DEV DEPLOY pipeline"
"Trigger a build for pipeline definition 299"
```

### YAML File Access
```
"Get the content of azure-pipelines.yml from the TADERATCS repository"
"Show me all YAML pipeline files in the Service Management Portal repo"
"Update the pipeline YAML for the DEV DEPLOY pipeline"
"Find all YAML files in the ACDMSX repository"
```

### Work Item Management
```
"Show me active work items in TADERATCS"
"Create a new bug for the login issue"
"Get details of work item 95608"
```

### Azure Storage Management
```
"List all my storage accounts"
"Show me the containers in mystorageaccount"
"List all blobs in the backups container"
"Upload this configuration data to settings/config.json in mystorageaccount"
"Download the content of data.json from the files container"
"Copy backup.zip from container-a to container-b in mystorageaccount"
"Generate a 2-hour download link for report.pdf in the reports container"
"Create a new container called archives with metadata"
```


## 🔧 Architecture

### Zero Configuration Discovery
The server automatically works with a simple local configuration file, bypassing:
- Complex environment variable setup
- System-wide credential storage issues
- Process context security restrictions

### Credential Management
- **Local File**: `devops-config.json` in executable directory
- **Secure**: File permissions control access
- **Simple**: One JSON file, no system configuration
- **Reliable**: No dependency on external credential systems

### Service Architecture
```
Authentication/
├── AzureCredentialManager.cs      # Azure Resource Manager credentials
├── DevOpsCredentialManager.cs     # Azure DevOps PAT authentication
└── AzureEnvironmentDiscovery.cs   # Credential discovery engine

Services/DevOps/
├── IDevOpsService.cs              # Service interface
├── DevOpsService.cs               # Core Azure DevOps operations
└── Models/                        # DTOs for projects, work items, pipelines

Tools/
└── DevOpsTools.cs                 # MCP tool implementations
```

## 🛡️ Security

- **Local Configuration**: Credentials stored in local file with file system permissions
- **No System Dependencies**: No Windows Credential Manager or environment variable requirements
- **Encrypted Communication**: All Azure DevOps API calls use HTTPS
- **No Credential Logging**: PAT tokens are never logged or exposed

## 🔍 Troubleshooting

### Configuration File Issues
1. **Verify file location**: Must be in same directory as `AzureMcp.exe`
2. **Check JSON format**: Ensure valid JSON syntax
3. **Validate PAT**: Test token in Azure DevOps web interface

### Authentication Issues
1. **PAT Permissions**: Verify token has required scopes
2. **Organization URL**: Use format `https://dev.azure.com/yourorg`
3. **PAT Expiration**: Check if token needs renewal

### Build Issues
- CredentialManagement package warnings are expected on .NET 9
- Serilog file logging warnings are non-critical
- Ensure all NuGet packages are restored

## 📈 Monitoring

Debug logs are automatically created in the executable directory:
- `azure-discovery.log`: Discovery process details
- `azure-discovery-YYYYMMDD.log`: Daily discovery logs

## 🎯 Benefits

- **Zero Configuration**: No system-wide settings required
- **AWS MCP-like Experience**: Just add to Claude config and go
- **Complete DevOps Access**: Projects, work items, repos, pipelines, YAML files
- **Secure**: Local file-based credential storage
- **Reliable**: No dependency on system credential managers
- **Extensible**: Easy to add new Azure DevOps capabilities

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Add new Azure DevOps capabilities using existing patterns
4. Test thoroughly with your own Azure DevOps environment
5. Submit a pull request

The authentication infrastructure supports any Azure DevOps REST API operations - just add new service methods and corresponding MCP tools.

## 📄 License

This project is part of the McpServers solution.