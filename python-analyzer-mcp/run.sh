#!/bin/bash
# run.sh - Python Analyzer MCP Server Launcher
# Automatically sets up virtual environment and runs the server

# Get the directory where this script is located
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Check if venv exists
if [ ! -f "$SCRIPT_DIR/.venv/bin/python" ]; then
    echo "[Python Analyzer MCP] Virtual environment not found. Creating..."
    python3 -m venv "$SCRIPT_DIR/.venv"
    
    if [ $? -ne 0 ]; then
        echo "[Python Analyzer MCP] ERROR: Failed to create virtual environment."
        echo "[Python Analyzer MCP] Please ensure Python 3.10+ is installed."
        exit 1
    fi
    
    echo "[Python Analyzer MCP] Installing dependencies..."
    "$SCRIPT_DIR/.venv/bin/pip" install -r "$SCRIPT_DIR/requirements.txt" > /dev/null 2>&1
    
    if [ $? -ne 0 ]; then
        echo "[Python Analyzer MCP] ERROR: Failed to install dependencies."
        exit 1
    fi
    
    echo "[Python Analyzer MCP] Setup complete!"
fi

# Change to script directory and run the server
cd "$SCRIPT_DIR"
"$SCRIPT_DIR/.venv/bin/python" -m src.main
