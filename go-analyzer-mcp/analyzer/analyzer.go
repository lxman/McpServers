package analyzer

import (
	"bytes"
	"fmt"
	"go/ast"
	"go/parser"
	"go/token"
	"os"
	"os/exec"
	"path/filepath"
)

// AnalyzeCodeInput represents the input for code analysis
type AnalyzeCodeInput struct {
	Code     string `json:"code" jsonschema:"Go source code to analyze"`
	FileName string `json:"fileName,omitempty" jsonschema:"Optional filename for context (default: temp.go)"`
}

// AnalyzeCodeOutput represents the result of code analysis
type AnalyzeCodeOutput struct {
	Success     bool         `json:"success"`
	Diagnostics []Diagnostic `json:"diagnostics"`
	ErrorCount  int          `json:"error_count"`
	WarningCount int          `json:"warning_count"`
}

// Diagnostic represents a single diagnostic message
type Diagnostic struct {
	File     string `json:"file"`
	Line     int    `json:"line"`
	Column   int    `json:"column"`
	Message  string `json:"message"`
	Severity string `json:"severity"` // "error" or "warning"
}

// AnalyzeCode runs go vet on the provided code
func AnalyzeCode(code, fileName string) (*AnalyzeCodeOutput, error) {
	if fileName == "" {
		fileName = "temp.go"
	}

	// Create temp file
	tempDir, err := os.MkdirTemp("", "go-analyzer-*")
	if err != nil {
		return nil, fmt.Errorf("failed to create temp dir: %w", err)
	}
	defer os.RemoveAll(tempDir)

	tempFile := filepath.Join(tempDir, fileName)
	if err := os.WriteFile(tempFile, []byte(code), 0644); err != nil {
		return nil, fmt.Errorf("failed to write temp file: %w", err)
	}

	// Run go vet
	cmd := exec.Command("go", "vet", tempFile)
	var stderr bytes.Buffer
	cmd.Stderr = &stderr

	_ = cmd.Run() // Ignore exit code, we'll parse stderr

	// Parse diagnostics
	diagnostics := parseVetOutput(stderr.String())

	errorCount := 0
	warningCount := 0
	for _, diag := range diagnostics {
		if diag.Severity == "error" {
			errorCount++
		} else {
			warningCount++
		}
	}

	return &AnalyzeCodeOutput{
		Success:      len(diagnostics) == 0,
		Diagnostics:  diagnostics,
		ErrorCount:   errorCount,
		WarningCount: warningCount,
	}, nil
}

// parseVetOutput parses go vet stderr output into diagnostics
func parseVetOutput(output string) []Diagnostic {
	if output == "" {
		return []Diagnostic{}
	}

	// go vet output format: "file:line:column: message"
	lines := bytes.Split([]byte(output), []byte("\n"))
	diagnostics := []Diagnostic{}

	for _, line := range lines {
		if len(line) == 0 {
			continue
		}

		// Simple parsing - can be enhanced
		diagnostics = append(diagnostics, Diagnostic{
			Message:  string(line),
			Severity: "error",
		})
	}

	return diagnostics
}

// ParseAST parses Go source code into an AST
func ParseAST(code string) (*ast.File, *token.FileSet, error) {
	fset := token.NewFileSet()
	file, err := parser.ParseFile(fset, "temp.go", code, parser.ParseComments)
	if err != nil {
		return nil, nil, fmt.Errorf("failed to parse code: %w", err)
	}
	return file, fset, nil
}