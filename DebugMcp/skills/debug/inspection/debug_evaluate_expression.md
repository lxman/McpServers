# Debug Evaluate Expression

## Description
Evaluates a debugger expression in the context of the current execution point. Can be used to inspect variable values, perform calculations, or call functions.

## Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| sessionId | string | Yes | Unique identifier of the session |
| expression | string | Yes | The expression to evaluate |

## Returns

Returns a JSON object with the following structure:

```json
{
  "result": "string",
  "expression": "string",
  "type": "string",
  "value": "string",
  "success": "boolean"
}
```

### Response Fields

| Field | Type | Description |
|-------|------|-------------|
| result | string | The result of expression evaluation |
| expression | string | The expression that was evaluated |
| type | string | Data type of the result |
| value | string | String representation of the result value |
| success | boolean | Whether the expression was successfully evaluated |

## Example

### Request
```json
{
  "sessionId": "session_12345",
  "expression": "a + b"
}
```

### Response
```json
{
  "result": "57",
  "expression": "a + b",
  "type": "int",
  "value": "57",
  "success": true
}
```
