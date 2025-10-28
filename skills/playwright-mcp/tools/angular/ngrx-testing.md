# NgRx State Management Testing

Test NgRx store actions, effects, and state management with comprehensive automation.

## Methods

### TestNgrxStoreActions
Test NgRx store actions, effects, and state management.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID for browser context
- `actionTypes` (string, default: ""): Specific action types to test (comma-separated, optional)
- `testEffects` (bool, default: true): Test effects along with actions
- `testReducers` (bool, default: true): Test reducers for state mutations
- `testSelectors` (bool, default: true): Test selectors for memoization and performance
- `validateStateStructure` (bool, default: true): Validate state structure and normalization
- `timeoutSeconds` (int, default: 60): Maximum execution time in seconds
- `generateRecommendations` (bool, default: true): Generate performance recommendations

**Returns:** string - JSON with NgRx testing results

## Use Cases

- Store action testing
- Effect chain validation
- Reducer state mutation testing
- Selector memoization testing
- State structure validation
- Performance optimization

## Best Practices

1. Test action -> effect -> reducer flow completely
2. Validate selector memoization for performance
3. Check state normalization patterns
4. Test error handling in effects
5. Monitor for state mutation bugs
6. Verify entity adapter usage
