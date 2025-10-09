# Go Analyzer MCP Server

A Model Context Protocol (MCP) server that provides Go code analysis capabilities for Claude.

## Features

### 🔍 Code Analysis
- **analyze_code**: Run `go vet` to check for common errors and suspicious constructs
- **get_symbols**: Extract functions, types, variables, and other symbols from Go code
- **calculate_metrics**: Calculate code complexity metrics including cyclomatic complexity, lines of code, and function counts
- **format_code**: Format Go code using `gofmt` standard formatting

## Tools Available

### 1. analyze_code
Analyzes Go code for errors and warnings using `go vet`.

**Parameters:**
- `code` (string, required): Go source code to analyze
- `fileName` (string, optional): Filename for context (default: "temp.go")

**Returns:**
- Success status
- List of diagnostics (errors/warnings)
- Error and warning counts

### 2. format_code
Formats Go code according to the standard Go formatting rules using `gofmt`.

**Parameters:**
- `code` (string, required): Go source code to format

**Returns:**
- Formatted code
- Success status

### 3. get_symbols
Extracts all symbols (functions, types, variables, constants, etc.) from Go code.

**Parameters:**
- `code` (string, required): Go source code to analyze
- `filter` (string, optional): Filter by symbol type ("function", "type", "variable", "all")

**Returns:**
- List of symbols with their names, kinds, signatures, and line numbers
- Total count of symbols found

### 4. calculate_metrics
Calculates various code metrics including complexity and size metrics.

**Parameters:**
- `code` (string, required): Go source code to analyze

**Returns:**
- Overall metrics (lines of code, comment lines, blank lines, function count, type count)
- Cyclomatic complexity (average and maximum)
- Per-function metrics (complexity and lines of code)

## Configuration

Add to your Claude Desktop config (`claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "go-analyzer": {
      "command": "C:\\Users\\jorda\\RiderProjects\\McpServers\\go-analyzer-mcp\\go-analyzer.exe"
    }
  }
}
```

## Building

```bash
go mod tidy
go build -o go-analyzer.exe
```

## Requirements

- Go 1.21 or higher
- `go vet` and `gofmt` (included with Go installation)

## Architecture

The server is built using:
- **go-sdk**: Anthropic's official Go SDK for MCP servers
- **go/ast**: Go's built-in AST parser for code analysis
- **go/token**: Position tracking and file set management
- **go vet**: Standard Go static analysis tool

## Example Usage

Once configured in Claude Desktop, you can ask Claude to:
- "Analyze this Go code for errors"
- "Format this Go code"
- "Extract all functions from this Go file"
- "Calculate complexity metrics for this code"
- "What are the exported types in this code?"

## Project Structure

```
go-analyzer-mcp/
├── analyzer/          # Core analysis functionality
│   ├── analyzer.go    # Main analysis (go vet)
│   ├── format.go      # Code formatting (gofmt)
│   ├── metrics.go     # Code metrics and complexity
│   └── symbols.go     # Symbol extraction
├── tools/             # MCP tool handlers
│   └── tools.go       # Tool registration and handlers
├── main.go            # Server entry point
├── go.mod             # Go module dependencies
└── go.sum             # Dependency checksums
```

## License

MIT
