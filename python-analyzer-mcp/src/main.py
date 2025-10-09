#!/usr/bin/env python3
"""Python Analyzer MCP Server - Main entry point"""
import asyncio
import json
from mcp.server import Server
from mcp.server.stdio import stdio_server
from mcp.types import Tool, TextContent

from .tools import PythonTools

# Create the Python tools instance
py_tools = PythonTools()

# Define the available tools
tools: list[Tool] = [
    Tool(
        name="analyze_code",
        description="Analyze Python code for errors, warnings, and compatibility issues with version awareness",
        inputSchema={
            "type": "object",
            "properties": {
                "code": {
                    "type": "string",
                    "description": "Python code to analyze"
                },
                "fileName": {
                    "type": "string",
                    "description": "Optional file name for context"
                },
                "pythonVersion": {
                    "type": "string",
                    "description": "Target Python version (e.g., '3.8', '3.10', 'auto'). Default: 'auto'"
                }
            },
            "required": ["code"]
        }
    ),
    Tool(
        name="get_symbols",
        description="Extract all symbols (classes, functions, variables) from Python code",
        inputSchema={
            "type": "object",
            "properties": {
                "code": {
                    "type": "string",
                    "description": "Python code to analyze"
                },
                "fileName": {
                    "type": "string",
                    "description": "Optional file name for context"
                },
                "filter": {
                    "type": "string",
                    "description": "Optional filter: 'class', 'function', 'variable', or 'all'"
                }
            },
            "required": ["code"]
        }
    ),
    Tool(
        name="format_code",
        description="Format Python code using black formatter",
        inputSchema={
            "type": "object",
            "properties": {
                "code": {
                    "type": "string",
                    "description": "Python code to format"
                }
            },
            "required": ["code"]
        }
    ),
    Tool(
        name="calculate_metrics",
        description="Calculate code metrics including cyclomatic complexity, maintainability index, and line counts",
        inputSchema={
            "type": "object",
            "properties": {
                "code": {
                    "type": "string",
                    "description": "Python code to analyze"
                },
                "fileName": {
                    "type": "string",
                    "description": "Optional file name for context"
                }
            },
            "required": ["code"]
        }
    ),
    Tool(
        name="type_check",
        description="Run static type checking using mypy to detect type errors and inconsistencies",
        inputSchema={
            "type": "object",
            "properties": {
                "code": {
                    "type": "string",
                    "description": "Python code to type check"
                },
                "fileName": {
                    "type": "string",
                    "description": "Optional file name for context"
                }
            },
            "required": ["code"]
        }
    ),
    Tool(
        name="detect_dead_code",
        description="Detect unused functions, classes, and variables using vulture",
        inputSchema={
            "type": "object",
            "properties": {
                "code": {
                    "type": "string",
                    "description": "Python code to analyze"
                },
                "fileName": {
                    "type": "string",
                    "description": "Optional file name for context"
                }
            },
            "required": ["code"]
        }
    ),
    Tool(
        name="comprehensive_lint",
        description="Run comprehensive linting using pylint for code quality analysis",
        inputSchema={
            "type": "object",
            "properties": {
                "code": {
                    "type": "string",
                    "description": "Python code to lint"
                },
                "fileName": {
                    "type": "string",
                    "description": "Optional file name for context"
                }
            },
            "required": ["code"]
        }
    ),
    Tool(
        name="get_completions",
        description="Get code completions at a specific position using jedi for intelligent autocomplete",
        inputSchema={
            "type": "object",
            "properties": {
                "code": {
                    "type": "string",
                    "description": "Python code"
                },
                "line": {
                    "type": "integer",
                    "description": "Line number (1-based)"
                },
                "column": {
                    "type": "integer",
                    "description": "Column number (0-based)"
                }
            },
            "required": ["code", "line", "column"]
        }
    ),
    Tool(
        name="format_with_autopep8",
        description="Format Python code using autopep8 as an alternative to black",
        inputSchema={
            "type": "object",
            "properties": {
                "code": {
                    "type": "string",
                    "description": "Python code to format"
                },
                "maxLineLength": {
                    "type": "integer",
                    "description": "Maximum line length (default: 79)"
                }
            },
            "required": ["code"]
        }
    )
]

# Create the MCP server
app = Server("python-analyzer-mcp")


@app.list_tools()
async def list_tools() -> list[Tool]:
    """List available tools"""
    return tools


@app.call_tool()
async def call_tool(name: str, arguments: dict) -> list[TextContent]:
    """Handle tool calls"""

    try:
        if name == "analyze_code":
            result = py_tools.analyze_code(
                code=arguments["code"],
                file_name=arguments.get("fileName"),
                python_version=arguments.get("pythonVersion", "auto")
            )
        elif name == "get_symbols":
            result = py_tools.get_symbols(
                code=arguments["code"],
                file_name=arguments.get("fileName"),
                filter=arguments.get("filter")
            )
        elif name == "format_code":
            result = py_tools.format_code(
                code=arguments["code"]
            )
        elif name == "calculate_metrics":
            result = py_tools.calculate_metrics(
                code=arguments["code"],
                file_name=arguments.get("fileName")
            )
        elif name == "type_check":
            result = py_tools.type_check(
                code=arguments["code"],
                file_name=arguments.get("fileName")
            )
        elif name == "detect_dead_code":
            result = py_tools.detect_dead_code(
                code=arguments["code"],
                file_name=arguments.get("fileName")
            )
        elif name == "comprehensive_lint":
            result = py_tools.comprehensive_lint(
                code=arguments["code"],
                file_name=arguments.get("fileName")
            )
        elif name == "get_completions":
            result = py_tools.get_completions(
                code=arguments["code"],
                line=arguments["line"],
                column=arguments["column"]
            )
        elif name == "format_with_autopep8":
            result = py_tools.format_with_autopep8(
                code=arguments["code"],
                max_line_length=arguments.get("maxLineLength")
            )
        else:
            result = {
                "success": False,
                "error": f"Unknown tool: {name}"
            }

        return [TextContent(
            type="text",
            text=json.dumps(result, indent=2)
        )]

    except Exception as e:
        return [TextContent(
            type="text",
            text=json.dumps({
                "success": False,
                "error": str(e)
            }, indent=2)
        )]


async def main():
    """Main entry point"""
    async with stdio_server() as (read_stream, write_stream):
        await app.run(
            read_stream,
            write_stream,
            app.create_initialization_options()
        )


if __name__ == "__main__":
    asyncio.run(main())