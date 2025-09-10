# AWS MCP Quick Start Guide

This guide will help you get the AWS MCP server up and running quickly.

## Prerequisites

1. **.NET 9.0 SDK** - Download from [Microsoft .NET](https://dotnet.microsoft.com/download)
2. **AWS Account** - Sign up at [AWS Console](https://aws.amazon.com/console/)
3. **AWS Credentials** - Configure using one of the methods below

## AWS Credentials Setup

Choose one of these methods to provide AWS credentials:

### Method 1: AWS CLI (Recommended)
```bash
# Install AWS CLI and configure
aws configure
```

### Method 2: Environment Variables
```bash
export AWS_ACCESS_KEY_ID=your_access_key
export AWS_SECRET_ACCESS_KEY=your_secret_key
export AWS_DEFAULT_REGION=us-east-1
```

### Method 3: AWS Profile
Create `~/.aws/credentials` file:
```ini
[default]
aws_access_key_id = your_access_key
aws_secret_access_key = your_secret_key
region = us-east-1
```

## Quick Start

1. **Clone and Build**
   ```bash
   cd C:\Users\jorda\RiderProjects\AwsMcp\AwsMcp
   dotnet build
   ```

2. **Run the MCP Server**
   ```bash
   dotnet run
   ```

3. **Test with LocalStack (Optional)**
   ```bash
   # Start LocalStack for local testing
   docker run --rm -it -p 4566:4566 localstack/localstack
   
   # Initialize with LocalStack endpoint
   InitializeS3(serviceUrl: "http://localhost:4566", forcePathStyle: true)
   ```

## Available Tools

### S3 Tools
- `InitializeS3` - Configure S3 connection
- `ListBuckets` - List all S3 buckets
- `ListObjects` - List objects in a bucket
- `GetObjectContent` - Download object content
- `PutObjectContent` - Upload text content
- `CreateBucket` - Create new bucket
- `DeleteBucket` - Delete empty bucket
- `GeneratePresignedUrl` - Create temporary access URLs

### CloudWatch Tools
- `InitializeCloudWatch` - Configure CloudWatch connection
- `ListMetrics` - List available metrics
- `GetMetricStatistics` - Get metric data points
- `PutMetricData` - Publish custom metrics
- `ListLogGroups` - List log groups
- `GetLogEvents` - Retrieve log events
- `FilterLogEvents` - Search logs with patterns

### ECS Tools
- `InitializeEcs` - Configure ECS connection
- `ListClusters` - List all ECS clusters
- `DescribeClusters` - Get detailed cluster information
- `CreateCluster` - Create new ECS cluster
- `ListServices` - List services in a cluster
- `DescribeServices` - Get detailed service information
- `RunTask` - Run a task on ECS cluster
- `StopTask` - Stop a running task
- `ListTaskDefinitions` - List available task definitions

### ECR Tools
- `InitializeEcr` - Configure ECR connection
- `ListRepositories` - List all ECR repositories
- `DescribeRepositories` - Get detailed repository information
- `CreateRepository` - Create new ECR repository
- `ListImages` - List images in a repository
- `DescribeImages` - Get detailed image information
- `GetAuthorizationToken` - Get Docker login token
- `BatchDeleteImages` - Delete multiple images
- `StartImageScan` - Start security scan

## Example Usage

### Initialize Services
```json
{
  "tool": "InitializeS3",
  "arguments": {
    "region": "us-east-1",
    "profileName": "default"
  }
}
```

```json
{
  "tool": "InitializeEcs",
  "arguments": {
    "region": "us-east-1"
  }
}
```

```json
{
  "tool": "InitializeEcr",
  "arguments": {
    "region": "us-east-1"
  }
}
```

### S3 Operations
```json
{
  "tool": "ListBuckets",
  "arguments": {}
}
```

```json
{
  "tool": "PutObjectContent",
  "arguments": {
    "bucketName": "my-bucket",
    "key": "test.txt",
    "content": "Hello, World!",
    "contentType": "text/plain"
  }
}
```

### ECS Operations
```json
{
  "tool": "CreateCluster",
  "arguments": {
    "clusterName": "my-cluster"
  }
}
```

```json
{
  "tool": "RunTask",
  "arguments": {
    "taskDefinition": "my-task:1",
    "cluster": "my-cluster",
    "launchType": "FARGATE"
  }
}
```

### ECR Operations
```json
{
  "tool": "CreateRepository",
  "arguments": {
    "repositoryName": "my-app",
    "imageScanOnPush": true
  }
}
```

```json
{
  "tool": "GetAuthorizationToken",
  "arguments": {}
}
```

### CloudWatch Operations
```json
{
  "tool": "ListMetrics",
  "arguments": {
    "namespaceName": "AWS/EC2",
    "maxRecords": 10
  }
}
```

## Configuration Options

Update `appsettings.json` for default settings:

```json
{
  "AwsConfiguration": {
    "Region": "us-east-1",
    "TimeoutSeconds": 30,
    "MaxRetryAttempts": 3,
    "UseHttps": true,
    "ForcePathStyle": false
  }
}
```

## Troubleshooting

### Common Issues

1. **Credentials Not Found**
   - Verify AWS credentials are properly configured
   - Check environment variables or AWS profile

2. **Region Access Denied**
   - Ensure your AWS account has access to the specified region
   - Verify IAM permissions for S3, CloudWatch, ECS, and ECR

3. **LocalStack Connection Issues**
   - Ensure LocalStack is running on port 4566
   - Use `forcePathStyle: true` for S3 operations
   - Note: ECS and ECR support in LocalStack may be limited

4. **ECS Task Errors**
   - Verify task definition exists and is valid
   - Check IAM roles for ECS task execution
   - Ensure cluster has available capacity

5. **ECR Authentication Issues**
   - Get fresh authorization token with `GetAuthorizationToken`
   - Verify ECR repository permissions

### Debug Mode

Enable detailed logging by updating `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "AwsMcp": "Debug"
    }
  }
}
```

## IAM Permissions

Your AWS credentials need the following permissions:

### S3 Permissions
```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "s3:ListBucket",
        "s3:GetObject",
        "s3:PutObject",
        "s3:DeleteObject",
        "s3:CreateBucket",
        "s3:DeleteBucket"
      ],
      "Resource": "*"
    }
  ]
}
```

### ECS Permissions
```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "ecs:*",
        "iam:PassRole"
      ],
      "Resource": "*"
    }
  ]
}
```

### ECR Permissions
```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "ecr:*"
      ],
      "Resource": "*"
    }
  ]
}
```

## Next Steps

1. Integrate with your MCP client
2. Explore advanced S3 operations (presigned URLs, multipart uploads)
3. Set up CloudWatch alarms and custom metrics
4. Deploy containerized applications with ECS
5. Manage container images with ECR
6. Configure IAM roles for production deployment

## Support

- Check the main README.md for detailed documentation
- Review AWS SDK documentation for advanced scenarios
- Test with LocalStack for local development
- Refer to AWS documentation for service-specific guidance
