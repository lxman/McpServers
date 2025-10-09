package tools

import (
	"context"
	"fmt"

	"github.com/jorda/go-analyzer-mcp/analyzer"
	"github.com/modelcontextprotocol/go-sdk/mcp"
)

// RegisterTools registers all Go analyzer tools with the MCP server
func RegisterTools(server *mcp.Server) {
	// Tool 1: Analyze Code (go vet)
	mcp.AddTool(server,
		&mcp.Tool{
			Name:        "analyze_code",
			Description: "Analyze Go code for errors and warnings using go vet",
		},
		handleAnalyzeCode,
	)

	// Tool 2: Format Code (gofmt)
	mcp.AddTool(server,
		&mcp.Tool{
			Name:        "format_code",
			Description: "Format Go code using gofmt",
		},
		handleFormatCode,
	)

	// Tool 3: Get Symbols
	mcp.AddTool(server,
		&mcp.Tool{
			Name:        "get_symbols",
			Description: "Extract symbols (functions, types, variables) from Go code",
		},
		handleGetSymbols,
	)

	// Tool 4: Calculate Metrics
	mcp.AddTool(server,
		&mcp.Tool{
			Name:        "calculate_metrics",
			Description: "Calculate code metrics including cyclomatic complexity and lines of code",
		},
		handleCalculateMetrics,
	)
}

// Tool Handlers

func handleAnalyzeCode(
	ctx context.Context,
	req *mcp.CallToolRequest,
	input analyzer.AnalyzeCodeInput,
) (*mcp.CallToolResult, any, error) {
	result, err := analyzer.AnalyzeCode(input.Code, input.FileName)
	if err != nil {
		return nil, nil, err
	}

	return &mcp.CallToolResult{
		Content: []mcp.Content{
			&mcp.TextContent{
				Text: formatAnalysisResult(result),
			},
		},
	}, result, nil
}

func handleFormatCode(
	ctx context.Context,
	req *mcp.CallToolRequest,
	input analyzer.FormatCodeInput,
) (*mcp.CallToolResult, any, error) {
	result, err := analyzer.FormatCode(input.Code)
	if err != nil {
		return nil, nil, err
	}

	if !result.Success {
		return nil, nil, fmt.Errorf("%s", result.Error)
	}

	return &mcp.CallToolResult{
		Content: []mcp.Content{
			&mcp.TextContent{
				Text: result.FormattedCode,
			},
		},
	}, result, nil
}

func handleGetSymbols(
	ctx context.Context,
	req *mcp.CallToolRequest,
	input analyzer.GetSymbolsInput,
) (*mcp.CallToolResult, any, error) {
	result, err := analyzer.GetSymbols(input.Code, input.Filter)
	if err != nil {
		return nil, nil, err
	}

	if !result.Success {
		return nil, nil, fmt.Errorf("%s", result.Error)
	}

	return &mcp.CallToolResult{
		Content: []mcp.Content{
			&mcp.TextContent{
				Text: formatSymbolsResult(result),
			},
		},
	}, result, nil
}

func handleCalculateMetrics(
	ctx context.Context,
	req *mcp.CallToolRequest,
	input analyzer.CalculateMetricsInput,
) (*mcp.CallToolResult, any, error) {
	result, err := analyzer.CalculateMetrics(input.Code)
	if err != nil {
		return nil, nil, err
	}

	if !result.Success {
		return nil, nil, fmt.Errorf("%s", result.Error)
	}

	return &mcp.CallToolResult{
		Content: []mcp.Content{
			&mcp.TextContent{
				Text: formatMetricsResult(result),
			},
		},
	}, result, nil
}

// Helper functions for formatting results

func formatAnalysisResult(result *analyzer.AnalyzeCodeOutput) string {
	if result.Success {
		return "✅ No issues found"
	}

	text := fmt.Sprintf("Found %d errors and %d warnings:\n\n", result.ErrorCount, result.WarningCount)
	for _, diag := range result.Diagnostics {
		text += fmt.Sprintf("[%s] %s\n", diag.Severity, diag.Message)
	}
	return text
}

func formatSymbolsResult(result *analyzer.GetSymbolsOutput) string {
	text := fmt.Sprintf("Found %d symbols:\n\n", result.Count)
	
	for _, sym := range result.Symbols {
		if sym.Signature != "" {
			text += fmt.Sprintf("%s: %s (line %d)\n", sym.Kind, sym.Signature, sym.Line)
		} else {
			text += fmt.Sprintf("%s: %s (line %d)\n", sym.Kind, sym.Name, sym.Line)
		}
	}
	
	return text
}

func formatMetricsResult(result *analyzer.CalculateMetricsOutput) string {
	m := result.Metrics
	text := fmt.Sprintf(`Code Metrics:
  Lines of Code: %d
  Comment Lines: %d
  Blank Lines: %d
  Function Count: %d
  Type Count: %d
  Average Complexity: %.2f
  Max Complexity: %d

`, m.LinesOfCode, m.CommentLines, m.BlankLines, m.FunctionCount, m.TypeCount, m.AverageComplexity, m.MaxComplexity)

	if len(result.FunctionMetrics) > 0 {
		text += "Function Metrics:\n"
		for _, fm := range result.FunctionMetrics {
			text += fmt.Sprintf("  %s (line %d): complexity=%d, loc=%d\n",
				fm.Name, fm.Line, fm.CyclomaticComplexity, fm.LinesOfCode)
		}
	}

	return text
}