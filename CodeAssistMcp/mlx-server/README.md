# MLX Embedding Server

Ollama-compatible embedding API for CodeAssist, using Apple Silicon MLX.
Serves `BAAI/bge-base-en-v1.5` (768 dimensions) on port 11435.

CodeAssist reaches this via `CodeAssist:OllamaUrl` in `CodeAssistMcp/appsettings.json`.

## Endpoints

| Method | Path             | Purpose                                    |
|--------|------------------|--------------------------------------------|
| POST   | `/api/embed`     | Generate embeddings (OllamaSharp calls this) |
| GET    | `/api/tags`      | List models (Ollama compatibility)          |
| GET    | `/health`        | Liveness plus loaded model name             |

The `model` field in a request is ignored: this is a single-model server. The
model is set by `app.state.model_id` in `server.py`.

## Setup

```bash
python3.14 -m venv .venv-embed
.venv-embed/bin/python -m pip install -r requirements.txt
.venv-embed/bin/python server.py --host 0.0.0.0 --port 11435
```

Run as a service with `com.mlx-embeddings.server.plist`: edit the paths to match
the machine, copy to `~/Library/LaunchAgents/`, then

```bash
launchctl bootstrap gui/$(id -u) ~/Library/LaunchAgents/com.mlx-embeddings.server.plist
```

## Use a dedicated venv

Install into a venv used only by this server. It previously shared one with
`mlx_lm.server` (port 11436) and `mlx_vlm.server` (port 8080), which pin
different fastapi/starlette majors. Upgrading dependencies for one service then
breaks another.

## Changing the model means reindexing

Existing Qdrant collections hold vectors produced by the current model. Swapping
`app.state.model_id` invalidates every stored vector, even if the new model also
emits 768 dimensions, because the vector spaces differ. Searches will return
plausible-looking but wrong results rather than failing outright. Reindex every
repository after any model change.

## Health checks that lie

`/health` and `/api/tags` respond from process state and stay green when the
model is loaded but embedding is broken. To actually verify the server:

```bash
curl -s -X POST http://<host>:11435/api/embed \
  -H 'Content-Type: application/json' \
  -d '{"model":"x","input":["test"]}'
```

Expect `embeddings` with one 768-element array. Anything under `detail` is a
failure, whatever `/health` says.
