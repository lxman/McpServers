# Mini Agent Architecture: Swappable Domain Adapters for Local AI

## The Core Idea

Run a large base model (Qwen3-32B) for general reasoning. Pair it with a small fine-tuned model (Qwen3-8B) that acts as a domain-specific filter — carrying judgment, preferences, and internalized patterns from accumulated experience. Swap the small model to change specializations. The base model stays the same; the adapter changes what it's good at.

Think of it as nature vs nurture made practical. The 32B model is nature — raw capability baked in at training time. The small model is nurture — everything learned from working in a specific domain with a specific person over time.

## Why Not Just Use Memory/RAG?

Memory retrieval (RAG, vector search, session logs) works but has limits:

- **Latency**: Every query requires a retrieval step before generation
- **Context window pressure**: Retrieved context competes with the actual task for token budget
- **Shallow integration**: The model "reads" retrieved knowledge rather than "knowing" it — the difference between consulting a reference book and having internalized the material
- **Fragile relevance**: Retrieval quality depends on query formulation; subtle domain intuition doesn't compress into a search query

Fine-tuning moves knowledge from "looked up" to "just knows." The small model carries patterns the base model would need explicit prompting to reproduce.

## Architecture

### Hardware Target

NVIDIA DGX Spark (GIGABYTE AI TOP ATOM):

- **GPU**: NVIDIA GB10 Grace Blackwell, compute capability 12.1, native FP8 support
- **Memory**: 128GB unified (shared CPU/GPU) — reported as 119GB usable
- **CPU**: 20 ARM cores (10x Cortex-X925 @ 3.9GHz + 10x Cortex-A725), aarch64
- **Storage**: 3.7TB NVMe
- **Driver**: 580.126.09, CUDA 13.0
- **Network**: 192.168.0.179, SSH key auth (id_ed25519)

### Model Selection: What We Evaluated

| Model | Size (loaded) | Coding Benchmarks | Notes |
|-------|--------------|-------------------|-------|
| DeepSeek-R1-Distill-Llama-70B | ~35-40GB FP8 | 57.5% LiveCodeBench | Chain-of-thought, but flagships are all 671B+ MoE — won't fit |
| Llama 3.3 70B | ~35-40GB FP8 | Good general | No specialized reasoning mode |
| **Qwen3-32B** | **~33GB FP8** | **Outperforms many 70Bs** | **Hybrid thinking/non-thinking modes, same family for adapter** |
| Qwen3-14B | ~8GB FP8 | Solid mid-range | Adapter candidate |
| **Qwen3-8B** | **~5GB Q8** | 60.2 LiveCodeBench | **Selected as adapter model** |

**Why Qwen3-32B over 70B-class models**: Benchmarks at or above the 70B class while using half the memory. Hybrid thinking mode lets it switch between deep reasoning and fast responses without the adapter needing to route. Leaves 2x the headroom for the adapter, KV cache, and OS.

**Why Qwen3 family for both**: Same tokenizer, same architecture, same training distribution. The two models "speak the same language" internally, making the pre/post processing pipeline more coherent.

### Runtime Layout (Validated)

| Component | Size | Role | Status |
|-----------|------|------|--------|
| Qwen3-32B (FP8) | ~33GB weights, ~87GB total w/ KV cache | General reasoning, code generation, tool use | Running |
| EAGLE-3 speculator | ~3GB | Speculative decoding draft head | Running |
| Qwen3-8B (Q8) domain adapter | ~5GB | Domain-specific judgment, style, conventions | Future |
| Desktop/RDP overhead | ~1GB | Headless GNOME + gnome-remote-desktop | Running |
| OS + services | ~5GB | Ubuntu 24.04, Docker | Running |
| **Free for adapter + headroom** | **~30-35GB** | Room for adapter model + its KV cache | Available |

### Unified Memory: Lessons Learned the Hard Way

The GB10 uses unified memory — there is NO separate VRAM. `--gpu-memory-utilization` in vLLM claims a percentage of ALL system memory, not just "GPU memory." Setting this too high (0.8-0.9) starves the OS and will lock the box hard — load average 72, OOM, requires physical power cycle.

**Safe settings**:

- `--gpu-memory-utilization 0.65` — leaves ~32GB for OS, desktop, and headroom
- Never go above 0.7 with the desktop running
- Monitor with `free -h` — watch "available" column, not "free"

Both models run simultaneously. The small model preprocesses or post-processes the base model's output. MCP tools go to the base model (stronger reasoning for tool selection and parameter construction). The adapter handles routing, domain context injection, and output evaluation.

### Interaction Patterns

Several ways the adapter can work with the base model:

1. **Routing/classification**: Small model triages incoming requests, selects prompts, prunes tool lists to domain-relevant subset, or decides what context to inject before the base model sees the task
2. **Speculative decoding**: EAGLE-3 speculator handles this role instead of the adapter (see below) — a purpose-built tiny neural network that predicts what the 32B would generate, delivering 2-3x speedup
3. **Post-processing filter**: Base model generates, small model evaluates/rewrites to match domain conventions ("that code works but violates the team's async patterns")
4. **Embedding bias**: Small model produces domain-aware embeddings that influence retrieval and context selection for the base model

### Speculative Decoding: EAGLE-3, Not Draft Models

**Original plan**: Use the 8B adapter as a draft model for speculative decoding — small model proposes tokens, large model validates in parallel.

**What we learned**: vLLM v0.10+ dropped support for classic draft-model speculative decoding (`NotImplementedError`). The supported methods are now EAGLE-3, n-gram, MTP, and Medusa.

**Current implementation**: `RedHatAI/Qwen3-32B-speculator.eagle3` — a 3GB purpose-trained speculator head. Much more efficient than running a full 8B draft model:

- Tiny memory footprint (3GB vs 5-8GB for 8B model)
- Higher acceptance rate (trained specifically for Qwen3-32B's distribution)
- No interference with the adapter's separate role as domain filter

**Future opportunity**: A domain-fine-tuned adapter would have even stronger domain priors than a generic 8B, but it serves a different purpose (pre/post processing). Speculative decoding and domain filtering are complementary, not competing.

### Tool Architecture: Base Model Holds the Tools

MCP tools belong on the 32B base model, not the adapter:

- Tool use is fundamentally a reasoning task (intent → tool selection → parameter construction → result interpretation → chaining)
- The 8B adapter lacks the capacity for complex multi-tool orchestration
- The adapter's role is to steer, not to drive: "you're in a .NET codebase, use the C# analyzer, not the Python one"

Think of it as a senior developer (base) with access to all the tooling, paired with a domain specialist (adapter) who steers tool selection and reviews output. You don't give the reviewer the keyboard.

## Building an Adapter: The Pipeline

### Phase 1: Experience Gathering (Idle Cognition)

A background daemon accumulates domain knowledge during active work:

- **Session summaries**: End-of-conversation extraction of decisions, patterns, corrections
- **Reflection cycles**: Periodic (every ~15 min) structured introspection against accumulated memory
- **Structured extraction templates**: DECISIONS, PATTERNS, CONNECTIONS, STALE, QUESTIONS
- **Memory decay**: Ebbinghaus-style forgetting curves so stale knowledge fades naturally
- **Storage**: Qdrant vector DB (already running at 192.168.0.170 for CodeAssist)

This is the "nurture" phase — the agent works, accumulates experience, and that experience gets stored as structured chunks.

### Phase 2: Distillation

Convert accumulated experience into training data:

1. **Knowledge chunks** → Question-answer pairs where the nurtured agent (base + memory retrieval) generates responses with full context
2. **Session transcripts** → Preference pairs (what worked vs what got corrected)
3. **Domain conventions** → Style/pattern examples extracted from successful interactions
4. **User preferences** → Communication style, code conventions, architectural preferences

The nurtured agent acts as teacher. It has access to all accumulated memory. A student model is trained to produce the same responses *without* retrieval — knowledge moves from external (retrieved) to internal (weights).

### Phase 3: QLoRA Fine-Tuning

Feasible on the GB10 (Qwen3-8B adapter):

- Adapter model loaded at 4-bit quantization: ~4GB
- LoRA adapters: 1-2GB
- Optimizer states: ~10GB
- Total: ~15GB — fits easily on the Spark when the base model is unloaded
- For Qwen3-14B (if 8B proves too small): ~25GB total, still fits

Training runs locally. No data leaves the machine. Each adapter trains on its own domain corpus.

**Note**: Fine-tuning ecosystem for Qwen3 is newer and less battle-tested than Qwen2.5. If QLoRA tooling is problematic on Qwen3-8B, Qwen2.5-7B-Instruct is a fallback with a mature fine-tuning ecosystem.

### Phase 4: Deployment

Swap adapters by loading a different small model alongside the base. Archive adapters when projects end. Spin up fresh ones for new domains.

## Example Adapters

### Snorkel Agent

- **Domain**: Python prompt evaluation, model output grading
- **Internalized knowledge**: Grading rubrics, common failure modes, voice-matching patterns, round budget management, transcript analysis patterns
- **Training data source**: Months of Snorkel Marlin evaluation sessions

### .NET Federal Agent

- **Domain**: Full-stack .NET development for government systems
- **Internalized knowledge**: Gov cloud constraints, FedRAMP compliance patterns, team coding conventions, regulatory requirements, specific framework versions
- **Training data source**: Federal project development sessions

### CodeAssist/Infrastructure Agent

- **Domain**: MCP server development, local AI infrastructure
- **Internalized knowledge**: Qdrant patterns, TreeSitter chunking, vLLM configuration, the specific codebase architecture
- **Training data source**: CodeAssist development and infrastructure work

## Existing Research and Projects

This isn't uncharted territory. Active work in the space as of early 2026:

### Production-Ready Systems

- **OpenClaw** (68K GitHub stars): Heartbeat daemon architecture for persistent agent state
- **Zep/Graphiti**: Temporal knowledge graphs with time-aware memory
- **Letta/MemGPT**: Tiered memory management for LLM agents
- **Google Vertex AI Memory Bank**: Cloud-native agent memory
- **Mem0**: Memory layer for AI applications

### Active Research (Jan-Feb 2026)

- **FadeMem**: Adaptive forgetting using Ebbinghaus decay curves
- **SYNAPSE**: Spreading activation networks for memory retrieval
- **AgeMem**: Age-aware memory management
- **MemoryOS/MemOS**: OS-level memory abstractions for agents
- **ACT-R-inspired memory**: Cognitive architecture patterns applied to LLMs

### Sleep/Dream Consolidation

- **NeuroDream**: Sleep-inspired memory consolidation
- **SleepNet/DreamNet**: Offline memory processing during idle time
- **Nature Communications (2025)**: Sleep-like replay patterns in transformers

### Academic

- **ICLR 2026 MemAgents Workshop** (April, Rio de Janeiro): Dedicated venue for agent memory research
- **Synthetic Entropy Clock**: Using entropy as a time proxy for temporal awareness
- **Stanford Generative Agents**: Simulated societies with memory and reflection

### Key Takeaways from the Research

- Forgetting is essential — unbounded memory degrades performance
- Temporal awareness must be explicit (transformers don't have it natively)
- Heartbeat architectures work in production
- Nobody has achieved genuine temporal *experience* yet, but the behavioral simulation is increasingly sophisticated

## Risks and Mitigations

| Risk | Mitigation |
| ---- | ---------- |
| Catastrophic forgetting during fine-tuning | QLoRA preserves base model weights; only adapter layers change |
| Overfitting to one user's patterns | Keep base model untouched; adapter is disposable if it goes wrong |
| Stale knowledge baked into weights | Periodic re-distillation from fresh experience; adapters are cheap to rebuild |
| Diminished general capability | Base model handles general reasoning; adapter only biases domain-specific tasks |
| Training data quality | Structured extraction templates ensure consistent, high-quality training examples |

## Current Infrastructure State

### What's Running (192.168.0.179)

| Service | Port | Container/Process | Notes |
| ------- | ---- | ----------------- | ----- |
| vLLM (Qwen3-32B FP8 + EAGLE-3) | 8000 | `vllm` Docker container | `--gpu-memory-utilization 0.65`, native tool calling enabled |
| mcpo (MCP-to-OpenAPI proxy) | 8001 | systemd service | Hot-reload config, serves MCP tools to Open WebUI |
| Open WebUI | 3000 | `open-webui` Docker container | Chat via vLLM (:8000), tools via mcpo (:8001) |
| DGX Dashboard | 11000 | systemd service | System monitoring, updates, JupyterLab |
| Headless RDP | 3389 | gnome-remote-desktop | 2560x1440, xorg.conf configured with Modeline + AllowNonEdidModes |

**Decommissioned services**:

- Ollama (systemd service) — disabled, models deleted. vLLM handles all inference.
- MCP Agent Proxy (port 8080) — superseded by mcpo + native vLLM tool calling.

### Docker Container: vLLM

```bash
docker run -d --name vllm \
  --gpus all \
  -p 8000:8000 \
  -v /home/lxman/models:/models \
  nvcr.io/nvidia/vllm:25.11-py3 \
  vllm serve /models/Qwen3-32B \
  --host 0.0.0.0 \
  --port 8000 \
  --quantization fp8 \
  --gpu-memory-utilization 0.65 \
  --speculative-config '{"model": "/models/Qwen3-32B-speculator.eagle3", "num_speculative_tokens": 5, "method": "eagle3"}' \
  --enable-auto-tool-choice \
  --tool-call-parser hermes
```

**Important**: Use `nvcr.io/nvidia/vllm:25.11-py3`, NOT `26.01-py3`. The 26.01 container requires driver 590+ but the Spark has driver 580. The 25.11 container is built for this hardware.

### MCP Tool Serving (mcpo)

MCP servers are exposed to Open WebUI via [mcpo](https://github.com/open-webui/mcpo), a lightweight proxy that wraps stdio-based MCP servers as OpenAPI endpoints.

**Config file**: `/home/lxman/.config/mcpo.json` — Claude Desktop format, hot-reloads on save.

**systemd service**: `mcpo.service` — enabled at boot, auto-restarts on failure.

**Adding a new MCP server**: Edit `/home/lxman/.config/mcpo.json`:

```json
{
  "mcpServers": {
    "server-name": {
      "command": "dotnet",
      "args": ["/home/lxman/RiderProjects/McpServers/ServerMcp/bin/Release/net10.0/ServerMcp.dll"]
    }
  }
}
```

Then add `http://192.168.0.179:8001/<server-name>/openapi.json` as a tool server in Open WebUI.

**Available .NET MCP servers** (all at `/home/lxman/RiderProjects/McpServers/<name>/bin/Release/net10.0/<name>.dll`):
AwsMcp, AzureMcp, CodeAssistMcp, CSharpAnalyzerMcp, DesktopCommanderMcp, DocumentMcp, EdgarMcp, McpUtilitiesServer, MongoMcp, PlaywrightServerMcp, RedisMcp, SeleniumMcp, SqlMcp, SshMcp, TrafficGenerator

**Non-.NET analyzers**: python-analyzer-mcp, go-analyzer-mcp, typescript-analyzer-mcp

### API Endpoints

- **vLLM** (with tool calling): `http://192.168.0.179:8000/v1/chat/completions` (model name: `/models/Qwen3-32B`)
- **mcpo tool servers**: `http://192.168.0.179:8001/<server-name>/openapi.json`

### Model Files on Disk

| Path                                               | Contents                                                  | Size |
| -------------------------------------------------- | --------------------------------------------------------- | ---- |
| `/home/lxman/models/Qwen3-32B/`                    | 17 safetensor shards (BF16, quantized to FP8 at runtime)  | 62GB |
| `/home/lxman/models/Qwen3-32B-speculator.eagle3/`  | EAGLE-3 speculator head                                   | 3GB  |

### Performance Benchmarks (Feb 2026)

Stress testing with concurrent 2048-token generation requests via vLLM:

| Concurrent Requests | Batch Time | Aggregate Throughput |
| ------------------- | ---------- | -------------------- |
| 2 | ~195s | ~21 tok/s |
| 4 | ~205s | ~40 tok/s |
| 8 | ~220s | ~75 tok/s |
| 16 | ~258s | ~127 tok/s |
| 32 | ~388s | ~169 tok/s (peak) |
| 48 | ~666s | ~148 tok/s |
| 64 | ~862s | ~152 tok/s |

**Thermal profile under sustained 32-concurrent load**: GPU 80°C, board 90°C, power ~70W (TDP 140W). Instant recovery on load drop. Passive cooling handles all workloads without throttling.

**KV cache concurrency**: vLLM reports max 4.05x concurrency for 40,960-token contexts at 65% memory utilization.

### Monitoring Tools

- `systemps` — custom script at `~/.local/bin/systemps`, shows GPU temp/power/utilization, NIC, NVMe, fan, and board temps via nvidia-smi + lm-sensors
- `stresstest [N]` — custom script at `~/.local/bin/stresstest`, fires N concurrent vLLM requests with batch timing

## Open Questions

- How often should adapters be re-distilled? Monthly? Per-project-phase?
- Can multiple adapters compose (load two small models for cross-domain work)?
- What's the minimum experience threshold before distillation produces useful results?
- Should the idle cognition daemon run the base model or a smaller dedicated model for reflection?
- When vLLM re-enables draft model support, would a fine-tuned 8B draft outperform the generic EAGLE-3 speculator for domain-specific generation?
- Optimal `num_speculative_tokens` — currently 5, worth benchmarking 3-8 range
- The old MCP Agent Proxy at port 8080 may still be running — can be stopped and removed if confirmed unused
