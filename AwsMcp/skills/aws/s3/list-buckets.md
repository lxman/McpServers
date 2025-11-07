# List S3 Buckets

List all S3 buckets in your AWS account.

## Parameters

None

## Returns

List of S3 buckets with their names and creation dates.

## Example Usage

```json
{}
```

## Response Example

```json
{
  "success": true,
  "bucketCount": 3,
  "buckets": [
    {
      "name": "my-bucket",
      "creationDate": "2024-01-01T00:00:00Z"
    }
  ]
}
```