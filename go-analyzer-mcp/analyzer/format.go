package analyzer

import (
	"bytes"
	"fmt"
	"go/format"
	"os/exec"
)

// FormatCodeInput represents the input for code formatting
type FormatCodeInput struct {
	Code string `json:"code" jsonschema:"Go source code to format"`
}

// FormatCodeOutput represents the result of code formatting
type FormatCodeOutput struct {
	Success        bool   `json:"success"`
	FormattedCode  string `json:"formatted_code,omitempty"`
	Error          string `json:"error,omitempty"`
}

// FormatCode formats Go code using gofmt
func FormatCode(code string) (*FormatCodeOutput, error) {
	// Try using go/format package first (faster, no subprocess)
	formatted, err := format.Source([]byte(code))
	if err == nil {
		return &FormatCodeOutput{
			Success:       true,
			FormattedCode: string(formatted),
		}, nil
	}

	// Fall back to gofmt command if go/format fails
	cmd := exec.Command("gofmt")
	cmd.Stdin = bytes.NewReader([]byte(code))
	
	var stdout, stderr bytes.Buffer
	cmd.Stdout = &stdout
	cmd.Stderr = &stderr

	if err := cmd.Run(); err != nil {
		return &FormatCodeOutput{
			Success: false,
			Error:   fmt.Sprintf("gofmt error: %v - %s", err, stderr.String()),
		}, nil
	}

	return &FormatCodeOutput{
		Success:       true,
		FormattedCode: stdout.String(),
	}, nil
}

// FormatCodeWithImports formats code and organizes imports using goimports if available
func FormatCodeWithImports(code string) (*FormatCodeOutput, error) {
	// Try goimports if available
	cmd := exec.Command("goimports")
	cmd.Stdin = bytes.NewReader([]byte(code))
	
	var stdout, stderr bytes.Buffer
	cmd.Stdout = &stdout
	cmd.Stderr = &stderr

	if err := cmd.Run(); err != nil {
		// Fall back to regular format if goimports not available
		return FormatCode(code)
	}

	return &FormatCodeOutput{
		Success:       true,
		FormattedCode: stdout.String(),
	}, nil
}
