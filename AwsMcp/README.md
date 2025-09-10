# AWS MCP Server

A Model Context Protocol (MCP) server for AWS services, providing easy access to Amazon S3, CloudWatch, ECS, and ECR operations through the MCP interface.

## Overview

This project provides an STDIO MCP server that exposes AWS functionality to MCP clients. It's designed following the same patterns as successful MCP projects, with a clean separation of concerns and comprehensive tool definitions.

## Architecture

The project is organized with the following structure:

```
AwsMcp/
├── Configuration/           # AWS configuration and credential handling
│   ├── AwsConfiguration.cs
│   └── AwsCredentialsProvider.cs
├── S3/                     # S3 service implementation
│   └── S3Service.cs
├── CloudWatch/             # CloudWatch service implementation
│   └── CloudWatchService.cs
├── ECS/                    # ECS service implementation
│   └── EcsService.cs
├── ECR/                    # ECR service implementation
│   └── EcrService.cs
├── Tools/                  # MCP tool definitions
│   ├── S3Tools.cs
│   ├── CloudWatchTools.cs
│   ├── EcsTools.cs
│   └── EcrTools.cs
├── Program.cs              # Application entry point
├── appsettings.json        # Configuration settings
└── AwsMcp.csproj          # Project file
```

## Features

### S3 Operations
- **Bucket Management**: List, create, delete buckets
- **Object Operations**: Upload, download, delete objects
- **Metadata**: Get object metadata and check existence
- **Presigned URLs**: Generate temporary access URLs
- **Content Management**: Handle text content and binary data

### CloudWatch Operations
- **Metrics**: List metrics, get statistics, publish custom metrics
- **Alarms**: Create and manage CloudWatch alarms
- **Logs**: Access log groups, streams, and filter log events
- **Real-time Monitoring**: Get current metric data and log events

### ECS Operations
- **Cluster Management**: Create, delete, and describe ECS clusters
- **Service Management**: List, describe, and update ECS services
- **Task Management**: Run, stop, and describe ECS tasks
- **Task Definitions**: List and describe task definitions
- **Container Instances**: List and describe container instances

### ECR Operations
- **Repository Management**: Create, delete, and describe repositories
- **Image Management**: List, describe, and delete container images
- **Authentication**: Get Docker login authorization tokens
- **Security**: Image scanning and vulnerability findings
- **Policies**: Repository and lifecycle policy management
- **Tagging**: Resource tagging and tag management

## Getting Started

### Prerequisites

- .NET 9.0 SDK
- AWS credentials configured (one of the following):
  - AWS CLI configured (`aws configure`)
  - Environment variables (`AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`)
  - IAM roles (for EC2 instances)
  - AWS credential file

### Installation

1. Clone the repository
2. Navigate to the AwsMcp directory
3. Build the project:
   ```bash
   dotnet build
   ```

### Configuration

The server supports multiple credential configuration methods:

#### 1. AWS Profile
```json
{
  "region": "us-east-1",
  "profileName": "default"
}
```

#### 2. Direct Credentials
```json
{
  "region": "us-east-1", 
  "accessKeyId": "your-access-key",
  "secretAccessKey": "your-secret-key"
}
```

#### 3. LocalStack (for testing)
```json
{
  "region": "us-east-1",
  "serviceUrl": "http://localhost:4566",
  "forcePathStyle": true
}
```

### Running

The server is designed to run as an STDIO MCP server:

```bash
dotnet run
```

## MCP Tools

### S3 Tools

| Tool | Description |
|------|-------------|
| `InitializeS3` | Initialize S3 service with credentials |
| `ListBuckets` | List all S3 buckets |
| `ListObjects` | List objects in a bucket |
| `GetObjectContent` | Get object content as text |
| `GetObjectMetadata` | Get object metadata |
| `PutObjectContent` | Upload text content to S3 |
| `DeleteObject` | Delete an object |
| `CreateBucket` | Create a new bucket |
| `DeleteBucket` | Delete an empty bucket |
| `GeneratePresignedUrl` | Generate temporary access URLs |
| `BucketExists` | Check if bucket exists |
| `ObjectExists` | Check if object exists |

### CloudWatch Tools

| Tool | Description |
|------|-------------|
| `InitializeCloudWatch` | Initialize CloudWatch service |
| `ListMetrics` | List available metrics |
| `GetMetricStatistics` | Get metric data points |
| `PutMetricData` | Publish custom metrics |
| `CreateAlarm` | Create CloudWatch alarms |
| `ListAlarms` | List existing alarms |
| `ListLogGroups` | List log groups |
| `ListLogStreams` | List log streams |
| `GetLogEvents` | Get log events from a stream |
| `FilterLogEvents` | Filter logs across streams |
| `CreateLogGroup` | Create a new log group |
| `DeleteLogGroup` | Delete a log group |

### ECS Tools

| Tool | Description |
|------|-------------|
| `InitializeEcs` | Initialize ECS service with credentials |
| `ListClusters` | List all ECS clusters |
| `DescribeClusters` | Get detailed cluster information |
| `CreateCluster` | Create a new ECS cluster |
| `DeleteCluster` | Delete an ECS cluster |
| `ListServices` | List services in a cluster |
| `DescribeServices` | Get detailed service information |
| `ListTasks` | List tasks in a cluster or service |
| `DescribeTasks` | Get detailed task information |
| `ListTaskDefinitions` | List available task definitions |
| `DescribeTaskDefinition` | Get detailed task definition |
| `RunTask` | Run a task on ECS cluster |
| `StopTask` | Stop a running task |
| `UpdateService` | Update an ECS service |
| `ListContainerInstances` | List container instances |

### ECR Tools

| Tool | Description |
|------|-------------|
| `InitializeEcr` | Initialize ECR service with credentials |
| `ListRepositories` | List all ECR repositories |
| `DescribeRepositories` | Get detailed repository information |
| `CreateRepository` | Create a new ECR repository |
| `DeleteRepository` | Delete an ECR repository |
| `ListImages` | List images in a repository |
| `DescribeImages` | Get detailed image information |
| `GetAuthorizationToken` | Get Docker login token |
| `BatchDeleteImages` | Delete multiple images |
| `GetRepositoryPolicy` | Get repository access policy |
| `SetRepositoryPolicy` | Set repository access policy |
| `DeleteRepositoryPolicy` | Delete repository policy |
| `DescribeImageScanFindings` | Get security scan results |
| `StartImageScan` | Start security scan |
| `GetLifecyclePolicy` | Get image lifecycle policy |
| `PutLifecyclePolicy` | Set image lifecycle policy |
| `DeleteLifecyclePolicy` | Delete lifecycle policy |
| `TagResource` | Tag ECR resources |
| `UntagResource` | Remove tags from resources |
| `ListTagsForResource` | List resource tags |

## Error Handling

The server includes comprehensive error handling with detailed error messages in JSON format. All operations return structured responses indicating success/failure status and relevant data.

## Security Considerations

- Console output is redirected to prevent JSON-RPC corruption
- Credentials are handled securely through AWS SDK best practices
- All logging to console/stdout is disabled to maintain MCP protocol integrity
- Supports temporary credentials and role-based access

## Development

### Adding New AWS Services

To add support for additional AWS services:

1. Create a new directory for the service (e.g., `Lambda/`)
2. Implement the service class following the existing patterns
3. Create corresponding tools in the `Tools/` directory
4. Register the service and tools in `Program.cs`
5. Add necessary AWS SDK packages to the project file

### Testing with LocalStack

The server supports LocalStack for local testing:

```bash
# Start LocalStack
docker run --rm -it -p 4566:4566 localstack/localstack

# Configure for LocalStack
InitializeS3(serviceUrl: "http://localhost:4566", forcePathStyle: true)
InitializeEcs(serviceUrl: "http://localhost:4566")
InitializeEcr(serviceUrl: "http://localhost:4566")
```

## Dependencies

- **AWS SDK**: Amazon S3, CloudWatch, CloudWatch Logs, ECS, ECR
- **MCP**: ModelContextProtocol for MCP server functionality
- **Microsoft.Extensions**: Configuration, hosting, dependency injection
- **.NET 9.0**: Latest .NET runtime

## Example Usage

### Initialize Services
```json
InitializeS3(region: "us-east-1")
InitializeEcs(region: "us-east-1")
InitializeEcr(region: "us-east-1")
```

### ECS Operations
```json
CreateCluster(clusterName: "my-cluster")
RunTask(taskDefinition: "my-task:1", cluster: "my-cluster", launchType: "FARGATE")
```

### ECR Operations
```json
CreateRepository(repositoryName: "my-app", imageScanOnPush: true)
GetAuthorizationToken()
```

## License

This project follows standard open-source practices. Please refer to your organization's licensing requirements.
