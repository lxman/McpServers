using McpGateway;

string repoRoot = Environment.GetEnvironmentVariable("MCP_GATEWAY_REPO_ROOT")
    ?? Directory.GetCurrentDirectory();

await GatewayApp.Build(GatewayApp.DefaultOptions(repoRoot)).RunAsync();
