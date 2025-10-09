package main

import (
	"context"
	"log"

	"github.com/jorda/go-analyzer-mcp/tools"
	"github.com/modelcontextprotocol/go-sdk/mcp"
)

func main() {
	// Create server with metadata
	server := mcp.NewServer(
		&mcp.Implementation{
			Name:    "go-analyzer",
			Version: "1.0.0",
		},
		nil, // No options yet
	)

	// Register all tools
	log.Println("Registering Go analyzer tools...")
	tools.RegisterTools(server)
	log.Println("Tools registered successfully")

	// Run server on stdio transport
	ctx := context.Background()
	transport := &mcp.StdioTransport{}
	
	log.Println("Starting Go analyzer MCP server...")
	if err := server.Run(ctx, transport); err != nil {
		log.Fatalf("Server error: %v", err)
	}
}