#!/usr/bin/env python3
"""
MLX Embedding Server - Ollama-compatible API for Apple Silicon optimized embeddings.
Uses mlx-embeddings for fast embedding generation on M-series Macs.
"""

import argparse
import logging
import time
from contextlib import asynccontextmanager
from typing import List, Union

import mlx.core as mx
from fastapi import FastAPI, HTTPException
from mlx_embeddings import load as load_model, generate
from pydantic import BaseModel

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

# Global model reference
model = None
tokenizer = None
model_name = None


class EmbedRequest(BaseModel):
    model: str
    input: Union[str, List[str]]


class EmbedResponse(BaseModel):
    model: str
    embeddings: List[List[float]]


class TagsResponse(BaseModel):
    models: List[dict]


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Load model on startup."""
    global model, tokenizer, model_name

    model_id = app.state.model_id
    logger.info(f"Loading model: {model_id}")

    start = time.time()
    model, tokenizer = load_model(model_id)
    model_name = model_id.split("/")[-1]
    elapsed = time.time() - start

    logger.info(f"Model loaded in {elapsed:.2f}s")
    yield

    logger.info("Shutting down")


app = FastAPI(title="MLX Embedding Server", lifespan=lifespan)
app.state.model_id = "BAAI/bge-base-en-v1.5"


@app.get("/api/tags")
async def list_models() -> TagsResponse:
    """List available models (Ollama compatibility)."""
    return TagsResponse(models=[{
        "name": model_name,
        "model": model_name,
        "size": 0,
        "digest": "",
        "details": {"family": "bert"}
    }])


@app.post("/api/embed")
async def embed(request: EmbedRequest) -> EmbedResponse:
    """Generate embeddings (Ollama-compatible endpoint)."""
    global model, tokenizer

    if model is None:
        raise HTTPException(status_code=503, detail="Model not loaded")

    # Normalize input to list
    texts = request.input if isinstance(request.input, list) else [request.input]

    if not texts:
        return EmbedResponse(model=model_name, embeddings=[])

    try:
        start = time.time()

        # Use mlx-embeddings generate function
        output = generate(model, tokenizer, texts)

        # Get the sentence embeddings (text_embeds)
        embeddings = output.text_embeds

        # Convert to Python lists and free MLX metal cache
        result = embeddings.tolist()
        del output, embeddings
        mx.clear_cache()

        elapsed = time.time() - start
        rate = len(texts) / elapsed if elapsed > 0 else 0
        logger.debug(f"Generated {len(texts)} embeddings in {elapsed:.3f}s ({rate:.1f}/sec)")

        return EmbedResponse(model=model_name, embeddings=result)

    except Exception as e:
        logger.error(f"Embedding error: {e}")
        raise HTTPException(status_code=500, detail=str(e))


@app.get("/health")
async def health():
    """Health check endpoint."""
    return {"status": "ok", "model": model_name, "backend": "mlx"}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="MLX Embedding Server")
    parser.add_argument("--host", default="127.0.0.1", help="Host to bind to")
    parser.add_argument("--port", type=int, default=11435, help="Port to bind to")
    parser.add_argument("--model", default="BAAI/bge-base-en-v1.5",
                        help="HuggingFace model ID")
    args = parser.parse_args()

    app.state.model_id = args.model

    import uvicorn
    uvicorn.run(app, host=args.host, port=args.port)
