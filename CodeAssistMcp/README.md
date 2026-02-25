# CodeAssist MCP Server

An MCP (Model Context Protocol) server that provides semantic code search capabilities for Claude Code. It indexes code repositories using local embeddings and stores them in a vector database for fast, natural language search.

## Purpose

This server bridges Claude Code with local AI infrastructure to enable:

- **Semantic Code Search**: Search codebases using natural language queries like "function that handles user authentication" instead of exact text matches
- **Code Similarity Detection**: Find similar code patterns, potential duplicates, or related implementations
- **Symbol Search**: Locate classes, methods, and properties by name with semantic context
- **Incremental Indexing**: Efficiently update indexes when code changes, only reprocessing modified files
- **Parallel Processing**: File chunking runs in parallel across all CPU cores for fast indexing

## Architecture

```text
Claude Code <--MCP--> CodeAssistMcp <--> MLX Server (embeddings, optimized for Apple Silicon)
                                    <--> Qdrant (vector storage)
```

- **MLX Embedding Server**: Apple Silicon optimized embedding generation using MLX framework (3x faster than Ollama)
- **Qdrant**: Vector database that stores embeddings and enables fast similarity search
- **CodeAssist.Core**: Handles parallel code chunking (with Roslyn-based parsing for C#), file tracking, and search orchestration

## Required Dependencies

### MLX Embedding Server (Recommended for Apple Silicon)

The included MLX server provides optimized embeddings for M-series Macs.

**Setup:**

```bash
cd mlx-server
python3 -m venv .venv
.venv/bin/pip install mlx-embeddings uvicorn fastapi

# Start the server (port 11435)
.venv/bin/python server.py --port 11435
```

The server uses `BAAI/bge-base-en-v1.5` model (768 dimensions, BERT-based).

**Auto-start on boot (macOS):**

```bash
# Copy the provided launchd plist
cp com.mlx-embeddings.server.plist ~/Library/LaunchAgents/
launchctl load ~/Library/LaunchAgents/com.mlx-embeddings.server.plist
```

### Alternative: Ollama

For non-Apple Silicon systems or if you prefer Ollama:

```bash
# Install
brew install ollama  # macOS

# Start and pull model
ollama serve
ollama pull nomic-embed-text
```

Then configure `OllamaUrl` to `http://localhost:11434` in appsettings.json.

### Qdrant

Qdrant is the vector database for storing and querying embeddings.

**Run with Docker:**

```bash
docker run -d -p 6333:6333 -p 6334:6334 \
  -v qdrant_storage:/qdrant/storage \
  --restart=always \
  --name qdrant \
  qdrant/qdrant
```

**Auto-start on macOS with Colima:**

```bash
# Start Colima (Docker runtime)
colima start

# The Qdrant container will auto-restart with --restart=always
```

## Configuration

The server uses these default settings (configurable via appsettings.json):

| Setting | Default | Description |
| ------- | ------- | ----------- |
| `OllamaUrl` | `http://localhost:11435` | Embedding server API (MLX or Ollama) |
| `QdrantUrl` | `http://localhost:6333` | Qdrant API endpoint |
| `EmbeddingModel` | `nomic-embed-text` | Model name (for display) |
| `VectorDimension` | `768` | Embedding vector size |
| `MaxChunkSize` | `2000` | Maximum characters per code chunk |

## Performance

On Apple M4 Pro (14 cores, 48GB RAM):

| Operation | Speed |
| --------- | ----- |
| MLX Embeddings | ~140 embeddings/sec |
| Ollama Embeddings | ~45 embeddings/sec |
| File Chunking | Parallel across all cores |

MLX is approximately **3x faster** than Ollama for embeddings on Apple Silicon.

## MCP Tools

### Health Tools

| Tool | Description |
| ---- | ----------- |
| `check_health` | Verify embedding server and Qdrant are running |
| `setup_services` | Get installation and setup instructions |
| `pull_embedding_model` | Download the embedding model (Ollama only) |

### Index Tools

| Tool | Description |
| ---- | ----------- |
| `index_repository` | Index a code repository for semantic search |
| `list_indexes` | List all indexed repositories with metadata |
| `get_index_status` | Get detailed status of a specific index |
| `delete_index` | Remove a repository index and its data |
| `refresh_index` | Incrementally update an existing index |

### Search Tools

| Tool | Description |
| ---- | ----------- |
| `search_code` | Semantic search using natural language queries |
| `find_similar_code` | Find code similar to a given snippet |
| `search_by_symbol` | Search for classes, methods, or properties by name |
| `explain_code_area` | Get code related to a concept with context |

## Usage with Claude Code

1. Ensure MLX server (or Ollama) and Qdrant are running
2. Add the server to Claude Code:

   ```bash
   claude mcp add --transport stdio codeassist-mcp --scope user -- \
     dotnet /path/to/CodeAssistMcp.dll
   ```

3. Restart Claude Code
4. Use `check_health` to verify the setup
5. Index a repository:

   ```text
   Use index_repository to index /path/to/your/project
   ```

6. Search the indexed code:

   ```text
   Use search_code to find "error handling for database connections" in my-project
   ```

## Supported Languages

The server includes intelligent code chunking for:

- **C#**: Roslyn-based parsing that extracts classes, methods, properties, and other symbols as separate chunks
- **Other languages**: Line-based chunking with configurable chunk sizes

## Excluded Patterns

By default, these patterns are excluded from indexing:

- Build outputs: `bin/`, `obj/`, `dist/`, `build/`, `target/`
- Dependencies: `node_modules/`, `packages/`, `.venv/`
- IDE files: `.vs/`, `.idea/`, `.git/`
- Generated files: `*.Designer.cs`, `*.generated.cs`, `*.min.js`
- Documentation: `skills/` (Claude Code skill docs)

## Building

```bash
dotnet build -c Release
```

The output DLL will be at `bin/Release/net10.0/CodeAssistMcp.dll`

## Logging

Logs are written to `~/logs/codeassist-mcp-{date}.log` (not stdout, since MCP uses stdio for communication).
