@echo off
REM run.bat - Python Analyzer MCP Server Launcher
REM Automatically sets up virtual environment and runs the server

SETLOCAL

REM Get the directory where this script is located
SET "SCRIPT_DIR=%~dp0"

REM Remove trailing backslash
IF %SCRIPT_DIR:~-1%==\ SET "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"

REM Check if venv exists
IF NOT EXIST "%SCRIPT_DIR%\.venv\Scripts\python.exe" (
    echo [Python Analyzer MCP] Virtual environment not found. Creating...
    python -m venv "%SCRIPT_DIR%\.venv"
    
    IF ERRORLEVEL 1 (
        echo [Python Analyzer MCP] ERROR: Failed to create virtual environment.
        echo [Python Analyzer MCP] Please ensure Python 3.10+ is installed and in PATH.
        exit /b 1
    )
    
    echo [Python Analyzer MCP] Installing dependencies...
    "%SCRIPT_DIR%\.venv\Scripts\pip.exe" install -r "%SCRIPT_DIR%\requirements.txt" > nul 2>&1
    
    IF ERRORLEVEL 1 (
        echo [Python Analyzer MCP] ERROR: Failed to install dependencies.
        exit /b 1
    )
    
    echo [Python Analyzer MCP] Setup complete!
)

REM Change to script directory and run the server
cd /d "%SCRIPT_DIR%"
"%SCRIPT_DIR%\.venv\Scripts\python.exe" -m src.main

ENDLOCAL
