# List S3 Objects

List objects in an S3 bucket with pagination support.

## Parameters

- **bucketName**: Name of the S3 bucket
- **prefix**: Filter objects by key prefix (optional)
- **maxKeys**: Maximum objects to return
- **continuationToken**: Token for pagination (optional)

## Returns

List of objects with their metadata.

## Example Usage

```json
{
  "bucketName": "my-bucket",
  "prefix": "logs/",
  "maxKeys": 100
}
```