# Angular Component Contract Testing

Validate Angular component inputs, outputs, and interfaces.

## Methods

### ValidateComponentContracts
Validate Angular component contracts including inputs, outputs, and interfaces.

**Parameters:**
- `componentSelector` (string, required): Component selector or data-testid to test
- `sessionId` (string, default: "default"): Session ID
- `validationScope` (string, default: "all"): Validation scope - inputs, outputs, interfaces, or all
- `timeoutSeconds` (int, default: 60): Maximum test execution time
- `includePerformanceTesting` (bool, default: true): Include performance testing
- `generateRecommendations` (bool, default: true): Generate improvement recommendations

**Returns:** string - JSON with ContractValidationResult

**Validates:**
- Input property types and bindings
- Output event emissions
- Interface compliance
- Contract violations
- Performance impact

## Example

```
playwright:validate_component_contracts \
  --componentSelector "app-user-profile" \
  --validationScope all \
  --sessionId test
```
