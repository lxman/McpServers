# Debug Get Variables

## Description
Retrieves all local variables and their values at the current execution point in the debugging session.

## Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| sessionId | string | Yes | Unique identifier of the session |

## Returns

Returns a JSON object with the following structure:

```json
{
  "variables": [
    {
      "name": "string",
      "value": "string",
      "type": "string",
      "scope": "string"
    }
  ],
  "variableCount": "integer"
}
```

### Response Fields

| Field | Type | Description |
|-------|------|-------------|
| variables | array | Array of variable objects |
| variables[].name | string | Name of the variable |
| variables[].value | string | Current value of the variable |
| variables[].type | string | Data type of the variable |
| variables[].scope | string | Scope of the variable (e.g., "local", "parameter", "global") |
| variableCount | integer | Total number of variables |

## Example

### Request
```json
{
  "sessionId": "session_12345"
}
```

### Response
```json
{
  "variables": [
    {
      "name": "a",
      "value": "42",
      "type": "int",
      "scope": "parameter"
    },
    {
      "name": "b",
      "value": "15",
      "type": "int",
      "scope": "parameter"
    },
    {
      "name": "result",
      "value": "57",
      "type": "int",
      "scope": "local"
    },
    {
      "name": "tempBuffer",
      "value": "0x00abc123",
      "type": "char*",
      "scope": "local"
    }
  ],
  "variableCount": 4
}
```
