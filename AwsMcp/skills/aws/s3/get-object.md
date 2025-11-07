# Get S3 Object Content

Retrieve the content of an S3 object as text.

## Parameters

- **bucketName**: Name of the S3 bucket
- **key**: Object key

## Returns

The text content of the object.

## Example Usage

```json
{
  "bucketName": "my-bucket",
  "key": "data/config.json"
}
```

## Note

This operation retrieves the object as text. Binary objects may not display correctly.