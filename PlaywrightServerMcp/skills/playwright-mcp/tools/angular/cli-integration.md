# Angular CLI Integration

Execute Angular CLI commands and generate artifacts.

## Methods

### ExecuteNgCommands
Execute Angular CLI commands and capture output.

**Parameters:**
- `command` (string, required): Angular CLI command (e.g., 'ng build', 'ng test', 'ng generate component my-comp')
- `sessionId` (string, default: "default"): Session ID for context
- `workingDirectory` (string, **required**): Absolute path to the Angular project root. No default -- see [Working directory](#working-directory).
- `timeoutSeconds` (int, default: 120): Timeout in seconds

**Returns:** string - JSON with CLI command result (stdout, stderr, exitCode)

---

### GenerateAngularArtifact
Generate Angular components, services, or other artifacts.

**Parameters:**
- `artifactType` (string, required): Type - component, service, module, directive, pipe, etc.
- `artifactName` (string, required): Name of the artifact
- `options` (string, default: ""): Additional CLI options (e.g., '--skip-tests', '--inline-style')
- `workingDirectory` (string, **required**): Absolute path to the Angular project root. No default -- see [Working directory](#working-directory).
- `sessionId` (string, default: "default"): Session ID for context

**Returns:** string - Generation result

---

### CheckAngularCliStatus
Check if Angular CLI is installed and get version.

**Parameters:**
- `workingDirectory` (string, **required**): Absolute path to the Angular project root. No default -- see [Working directory](#working-directory).
- `sessionId` (string, default: "default"): Session ID for context

**Returns:** string - JSON with CLI version info

---

### BuildAngularProject
Build Angular project with specified configuration.

**Parameters:**
- `configuration` (string, default: "development"): Build configuration - development, production, or custom
- `options` (string, default: ""): Additional build options (e.g., '--watch', '--aot')
- `workingDirectory` (string, **required**): Absolute path to the Angular project root. No default -- see [Working directory](#working-directory).
- `sessionId` (string, default: "default"): Session ID for context

**Returns:** string - Build result

---

### ExecuteAngularUnitTests
Execute Angular unit tests with coverage.

**Parameters:**
- `workingDirectory` (string, **required**): Absolute path to the Angular project root. No default -- see [Working directory](#working-directory).
- `sessionId` (string, default: "default"): Session ID for context
- `browser` (string, default: "chrome"): Browser - chrome, firefox, edge
- `testPattern` (string, default: ""): Specific test pattern or file
- `mode` (string, default: "single-run"): Test mode - watch, ci, or custom
- `codeCoverage` (bool, default: true): Enable code coverage

**Returns:** string - JSON with test results

## Working directory

`workingDirectory` is **required** on every tool here, and must be an **absolute** path to your
Angular project root.

It used to fall back to the process's current directory. This server now runs as a shared HTTP
backend out of a versioned deploy directory, so that fallback silently aimed these tools at the
server's own install rather than at your project -- reporting on it, building in it, and in the
case of `ng generate` writing into it. A blank, relative, or non-existent path is now refused with
a message saying what to pass instead.
