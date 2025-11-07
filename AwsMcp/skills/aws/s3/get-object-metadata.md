# Get S3 Object Metadata

Retrieve metadata for an S3 object without downloading its content.

## Parameters

- **bucketName**: Name of the S3 bucket
- **key**: Object key

## Returns

Object metadata including size, content type, last modified date, and ETag.

## Example Usage

```json
{
  "bucketName": "my-bucket",
  "key": "data/file.txt"
}
```