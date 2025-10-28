# Angular Testing Integration

Execute Angular unit tests with comprehensive result parsing and Karma/Jasmine integration.

## Methods

### ExecuteAngularUnitTests
Execute Angular unit tests with comprehensive result parsing and analysis.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID for context
- `workingDirectory` (string, default: ""): Working directory (defaults to current directory)
- `testPattern` (string, default: ""): Specific test pattern or file to run (optional)
- `mode` (string, default: "single-run"): Test execution configuration
  - `"single-run"`: Run tests once and exit
  - `"watch"`: Watch mode for development
  - `"ci"`: CI mode with specific configurations
  - Custom configuration name from angular.json
- `browser` (string, default: "chrome"): Browser to use for testing (chrome, firefox, edge)
- `codeCoverage` (bool, default: true): Enable code coverage

**Returns:** string - JSON with unit test execution results

---

### CheckAngularCliStatus
Check if Angular CLI is installed and get version information.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID for context
- `workingDirectory` (string, default: ""): Working directory to check (defaults to current directory)

**Returns:** string - JSON with Angular CLI status

## Use Cases

- Automated unit test execution
- CI/CD pipeline integration
- Code coverage analysis
- Test result parsing
- Angular CLI version checking
- Test suite validation

## Test Execution Modes

1. **Single-Run**: Execute tests once, get results, exit
2. **Watch Mode**: Continuous testing during development
3. **CI Mode**: Optimized for continuous integration
4. **Custom**: Use named configurations from angular.json

## Best Practices

1. Always enable code coverage in CI
2. Use single-run mode for automation
3. Verify Angular CLI is installed first
4. Specify test patterns for focused testing
5. Use headless Chrome in CI environments
6. Parse test results for actionable insights
