@echo off
cd /d "%~dp0"
echo Starting Python Analyzer HTTP Server on port 7301...
echo Current directory: %cd%
echo.

REM Check if venv exists, create if needed
IF NOT EXIST ".venv\Scripts\python.exe" (
    echo Creating virtual environment...
    python -m venv .venv
)

echo Installing/updating dependencies...
.venv\Scripts\pip.exe install -r requirements.txt
echo.

echo Starting server with virtual environment...
.venv\Scripts\python.exe http_server.py
