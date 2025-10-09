# Python Analyzer MCP Server - Installation Guide

This guide will help you install and configure the Python Analyzer MCP server for Claude Desktop.

## Prerequisites

- **Python 3.10 or higher** installed on your system
- **Claude Desktop** application

## Quick Installation (Recommended)

The easiest way to get started is using the included launcher scripts. They automatically handle all setup including creating a virtual environment and installing dependencies.

### Windows Installation

1. **Download/Clone** this repository to your computer

2. **Add to Claude Desktop config**:
   - Open: `%APPDATA%\Roaming\Claude\claude_desktop_config.json`
   - Add the following (replace the path with your actual path):

   ```json
   {
     "mcpServers": {
       "python-analyzer": {
         "command": "C:\\Users\\YourName\\python-analyzer-mcp\\run.bat"
       }
     }
   }
   ```

3. **Restart Claude Desktop**
   - The launcher script will automatically:
     - Create a virtual environment (`.venv`)
     - Install all required dependencies
     - Start the server

4. **Done!** The server is now ready to use.

### macOS/Linux Installation

1. **Download/Clone** this repository to your computer

2. **Make the launcher executable**:
   ```bash
   cd python-analyzer-mcp
   chmod +x run.sh
   ```

3. **Add to Claude Desktop config**:
   - Open: `~/Library/Application Support/Claude/claude_desktop_config.json` (macOS)
   - Or: `~/.config/Claude/claude_desktop_config.json` (Linux)
   - Add the following (replace the path with your actual path):

   ```json
   {
     "mcpServers": {
       "python-analyzer": {
         "command": "/Users/YourName/python-analyzer-mcp/run.sh"
       }
     }
   }
   ```

4. **Restart Claude Desktop**
   - The launcher script will automatically:
     - Create a virtual environment (`.venv`)
     - Install all required dependencies
     - Start the server

5. **Done!** The server is now ready to use.

## Manual Installation (Advanced Users)

If you prefer to manage the Python environment yourself:

### 1. Create Virtual Environment

```bash
cd python-analyzer-mcp
python -m venv .venv
```

### 2. Activate Virtual Environment

**Windows:**
```cmd
.venv\Scripts\activate
```

**macOS/Linux:**
```bash
source .venv/bin/activate
```

### 3. Install Dependencies

```bash
pip install -r requirements.txt
```

### 4. Configure Claude Desktop

**Windows:**
```json
{
  "mcpServers": {
    "python-analyzer": {
      "command": "C:\\Users\\YourName\\python-analyzer-mcp\\.venv\\Scripts\\python.exe",
      "args": ["-m", "src.main"],
      "cwd": "C:\\Users\\YourName\\python-analyzer-mcp"
    }
  }
}
```

**macOS/Linux:**
```json
{
  "mcpServers": {
    "python-analyzer": {
      "command": "/Users/YourName/python-analyzer-mcp/.venv/bin/python",
      "args": ["-m", "src.main"],
      "cwd": "/Users/YourName/python-analyzer-mcp"
    }
  }
}
```

## Verifying Installation

After restarting Claude Desktop, you can verify the installation by asking Claude:

> "Can you analyze this Python code for errors?"

If the server is working correctly, Claude will be able to analyze Python code using the `analyze_code` tool.

## Troubleshooting

### "Python not found" error

**Solution:** Ensure Python 3.10+ is installed and in your system PATH.

**Windows:** Download from [python.org](https://www.python.org/downloads/) and select "Add Python to PATH" during installation.

**macOS:** Install via Homebrew: `brew install python@3.12`

**Linux:** Install via package manager: `sudo apt install python3.12` (Ubuntu/Debian)

### "spawn python ENOENT" error

**Solution:** Use the full path to the launcher script in your config, not a relative path.

### Virtual environment not created

**Solution:** Check that you have write permissions in the directory and Python's `venv` module is installed.

### Dependencies fail to install

**Solution:** Ensure you have an active internet connection. Try upgrading pip first:
```bash
python -m pip install --upgrade pip
```

## Updating

To update the server:

1. Pull the latest changes from the repository
2. Delete the `.venv` folder
3. Restart Claude Desktop - the launcher script will recreate everything

## Uninstallation

1. Remove the server entry from `claude_desktop_config.json`
2. Restart Claude Desktop
3. Delete the `python-analyzer-mcp` folder

## Support

For issues, please check:
- [README.md](README.md) for usage documentation
- Ensure Python 3.10+ is installed
- Check Claude Desktop logs for error messages
