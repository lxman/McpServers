package analyzer

import (
	"go/ast"
	"go/token"
	"strings"
)

// CalculateMetricsInput represents the input for metrics calculation
type CalculateMetricsInput struct {
	Code string `json:"code" jsonschema:"Go source code to analyze"`
}

// CalculateMetricsOutput represents the result of metrics calculation
type CalculateMetricsOutput struct {
	Success              bool              `json:"success"`
	Metrics              *CodeMetrics      `json:"metrics,omitempty"`
	FunctionMetrics      []FunctionMetrics `json:"function_metrics,omitempty"`
	Error                string            `json:"error,omitempty"`
}

// CodeMetrics represents overall code metrics
type CodeMetrics struct {
	LinesOfCode          int     `json:"lines_of_code"`
	CommentLines         int     `json:"comment_lines"`
	BlankLines           int     `json:"blank_lines"`
	FunctionCount        int     `json:"function_count"`
	TypeCount            int     `json:"type_count"`
	AverageComplexity    float64 `json:"average_complexity"`
	MaxComplexity        int     `json:"max_complexity"`
	TotalComplexity      int     `json:"total_complexity"`
}

// FunctionMetrics represents metrics for a single function
type FunctionMetrics struct {
	Name               string `json:"name"`
	Line               int    `json:"line"`
	CyclomaticComplexity int    `json:"cyclomatic_complexity"`
	LinesOfCode        int    `json:"lines_of_code"`
}

// CalculateMetrics calculates code metrics
func CalculateMetrics(code string) (*CalculateMetricsOutput, error) {
	file, fset, err := ParseAST(code)
	if err != nil {
		return &CalculateMetricsOutput{
			Success: false,
			Error:   err.Error(),
		}, nil
	}

	metrics := &CodeMetrics{}
	functionMetrics := []FunctionMetrics{}

	// Count lines
	lines := strings.Split(code, "\n")
	metrics.LinesOfCode = len(lines)
	
	for _, line := range lines {
		trimmed := strings.TrimSpace(line)
		if trimmed == "" {
			metrics.BlankLines++
		} else if strings.HasPrefix(trimmed, "//") || strings.HasPrefix(trimmed, "/*") {
			metrics.CommentLines++
		}
	}

	// Count types and functions
	ast.Inspect(file, func(n ast.Node) bool {
		switch decl := n.(type) {
		case *ast.FuncDecl:
			metrics.FunctionCount++
			
			// Calculate cyclomatic complexity for this function
			complexity := calculateComplexity(decl)
			metrics.TotalComplexity += complexity
			
			if complexity > metrics.MaxComplexity {
				metrics.MaxComplexity = complexity
			}

			pos := fset.Position(decl.Pos())
			end := fset.Position(decl.End())
			
			functionMetrics = append(functionMetrics, FunctionMetrics{
				Name:                 decl.Name.Name,
				Line:                 pos.Line,
				CyclomaticComplexity: complexity,
				LinesOfCode:          end.Line - pos.Line + 1,
			})

		case *ast.GenDecl:
			if decl.Tok == token.TYPE {
				metrics.TypeCount++
			}
		}
		return true
	})

	// Calculate average complexity
	if metrics.FunctionCount > 0 {
		metrics.AverageComplexity = float64(metrics.TotalComplexity) / float64(metrics.FunctionCount)
	}

	return &CalculateMetricsOutput{
		Success:         true,
		Metrics:         metrics,
		FunctionMetrics: functionMetrics,
	}, nil
}

// calculateComplexity calculates cyclomatic complexity for a function
func calculateComplexity(fn *ast.FuncDecl) int {
	complexity := 1 // Base complexity

	ast.Inspect(fn.Body, func(n ast.Node) bool {
		switch n.(type) {
		case *ast.IfStmt:
			complexity++
		case *ast.ForStmt, *ast.RangeStmt:
			complexity++
		case *ast.CaseClause:
			complexity++
		case *ast.CommClause:
			complexity++
		case *ast.BinaryExpr:
			// Count logical operators (&&, ||)
			if be, ok := n.(*ast.BinaryExpr); ok {
				if be.Op == token.LAND || be.Op == token.LOR {
					complexity++
				}
			}
		}
		return true
	})

	return complexity
}
