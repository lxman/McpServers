# List Metric Namespaces

List all available CloudWatch metric namespaces.

## Parameters

None

## Returns

List of unique metric namespaces.

## Example Usage

```json
{}
```

## Response Example

```json
{
  "success": true,
  "namespaceCount": 5,
  "namespaces": [
    "AWS/Lambda",
    "AWS/EC2",
    "AWS/RDS",
    "AWS/S3",
    "MyApp/Performance"
  ]
}
```