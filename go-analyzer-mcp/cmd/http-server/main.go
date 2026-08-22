package main

import (
	"encoding/json"
	"log"
	"net/http"

	"github.com/jorda/go-analyzer-mcp/analyzer"
	_ "github.com/jorda/go-analyzer-mcp/docs" // Import generated docs
	httpSwagger "github.com/swaggo/http-swagger"
)

const serverPort = "7300"

// @title Go Analyzer API
// @version 1.0
// @description Go code analysis tools with auto-generated OpenAPI documentation
// @host localhost:7300
// @BasePath /
func main() {
	http.HandleFunc("/description", handleDescription)
	http.HandleFunc("/api/go/analyze", handleAnalyzeCode)
	http.HandleFunc("/api/go/format", handleFormatCode)
	http.HandleFunc("/api/go/symbols", handleGetSymbols)
	http.HandleFunc("/api/go/metrics", handleCalculateMetrics)

	// Swagger UI
	http.Handle("/docs/", httpSwagger.WrapHandler)

	log.Printf("Go Analyzer HTTP Server starting on port %s", serverPort)
	log.Printf("OpenAPI documentation available at: http://localhost:%s/description", serverPort)
	log.Printf("Swagger UI available at: http://localhost:%s/docs/", serverPort)
	if err := http.ListenAndServe(":"+serverPort, nil); err != nil {
		log.Fatalf("Server error: %v", err)
	}
}

// handleDescription returns the auto-generated OpenAPI spec
// @Summary Get OpenAPI specification
// @Description Returns the complete OpenAPI 3.0 specification
// @Tags Documentation
// @Produce json
// @Success 200 {object} map[string]interface{}
// @Router /description [get]
func handleDescription(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodGet {
		http.Error(w, "Method not allowed", http.StatusMethodNotAllowed)
		return
	}

	// Serve the generated swagger.json
	http.ServeFile(w, r, "./docs/swagger.json")
}

// handleAnalyzeCode analyzes Go code for errors and warnings
// @Summary Analyze Go code
// @Description Analyze Go code for errors and warnings using go vet
// @Tags Go Analyzer
// @Accept json
// @Produce json
// @Param request body analyzer.AnalyzeCodeInput true "Code to analyze"
// @Success 200 {object} analyzer.AnalyzeCodeOutput
// @Failure 400 {object} map[string]interface{}
// @Failure 500 {object} map[string]interface{}
// @Router /api/go/analyze [post]
func handleAnalyzeCode(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		http.Error(w, "Method not allowed", http.StatusMethodNotAllowed)
		return
	}

	var input analyzer.AnalyzeCodeInput
	if err := json.NewDecoder(r.Body).Decode(&input); err != nil {
		respondError(w, "Invalid request body", http.StatusBadRequest)
		return
	}

	result, err := analyzer.AnalyzeCode(input.Code, input.FileName)
	if err != nil {
		respondError(w, err.Error(), http.StatusInternalServerError)
		return
	}

	respondJSON(w, result)
}

// handleFormatCode formats Go code
// @Summary Format Go code
// @Description Format Go code using gofmt
// @Tags Go Analyzer
// @Accept json
// @Produce json
// @Param request body analyzer.FormatCodeInput true "Code to format"
// @Success 200 {object} analyzer.FormatCodeOutput
// @Failure 400 {object} map[string]interface{}
// @Failure 500 {object} map[string]interface{}
// @Router /api/go/format [post]
func handleFormatCode(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		http.Error(w, "Method not allowed", http.StatusMethodNotAllowed)
		return
	}

	var input analyzer.FormatCodeInput
	if err := json.NewDecoder(r.Body).Decode(&input); err != nil {
		respondError(w, "Invalid request body", http.StatusBadRequest)
		return
	}

	result, err := analyzer.FormatCode(input.Code)
	if err != nil {
		respondError(w, err.Error(), http.StatusInternalServerError)
		return
	}

	respondJSON(w, result)
}

// handleGetSymbols extracts symbols from Go code
// @Summary Extract symbols
// @Description Extract symbols (functions, types, variables) from Go code
// @Tags Go Analyzer
// @Accept json
// @Produce json
// @Param request body analyzer.GetSymbolsInput true "Code to analyze"
// @Success 200 {object} analyzer.GetSymbolsOutput
// @Failure 400 {object} map[string]interface{}
// @Failure 500 {object} map[string]interface{}
// @Router /api/go/symbols [post]
func handleGetSymbols(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		http.Error(w, "Method not allowed", http.StatusMethodNotAllowed)
		return
	}

	var input analyzer.GetSymbolsInput
	if err := json.NewDecoder(r.Body).Decode(&input); err != nil {
		respondError(w, "Invalid request body", http.StatusBadRequest)
		return
	}

	result, err := analyzer.GetSymbols(input.Code, input.Filter)
	if err != nil {
		respondError(w, err.Error(), http.StatusInternalServerError)
		return
	}

	respondJSON(w, result)
}

// handleCalculateMetrics calculates code metrics
// @Summary Calculate metrics
// @Description Calculate code metrics including cyclomatic complexity
// @Tags Go Analyzer
// @Accept json
// @Produce json
// @Param request body analyzer.CalculateMetricsInput true "Code to analyze"
// @Success 200 {object} analyzer.CalculateMetricsOutput
// @Failure 400 {object} map[string]interface{}
// @Failure 500 {object} map[string]interface{}
// @Router /api/go/metrics [post]
func handleCalculateMetrics(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		http.Error(w, "Method not allowed", http.StatusMethodNotAllowed)
		return
	}

	var input analyzer.CalculateMetricsInput
	if err := json.NewDecoder(r.Body).Decode(&input); err != nil {
		respondError(w, "Invalid request body", http.StatusBadRequest)
		return
	}

	result, err := analyzer.CalculateMetrics(input.Code)
	if err != nil {
		respondError(w, err.Error(), http.StatusInternalServerError)
		return
	}

	respondJSON(w, result)
}

func respondJSON(w http.ResponseWriter, data interface{}) {
	w.Header().Set("Content-Type", "application/json")
	json.NewEncoder(w).Encode(data)
}

func respondError(w http.ResponseWriter, message string, statusCode int) {
	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(statusCode)
	json.NewEncoder(w).Encode(map[string]interface{}{
		"success": false,
		"error":   message,
	})
}
