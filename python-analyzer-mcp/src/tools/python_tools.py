"""MCP tool handlers for Python analysis"""
from typing import Dict, Any, Optional
from services import PythonAnalyzer


class PythonTools:
    """MCP tools for Python code analysis"""

    def __init__(self):
        self.analyzer = PythonAnalyzer()

    def analyze_code(self, code: str, file_name: Optional[str] = None,
                     python_version: Optional[str] = None) -> Dict[str, Any]:
        """
        Analyze Python code for errors, warnings, and compatibility issues
        
        Args:
            code: Python code to analyze
            file_name: Optional filename for context
            python_version: Target Python version (e.g., "3.8", "3.10", or "auto")
        
        Returns:
            Analysis results with diagnostics
        """
        file_name = file_name or "temp.py"

        # Parse target version
        target_version = None
        if python_version and python_version != "auto":
            try:
                parts = python_version.split('.')
                target_version = (int(parts[0]), int(parts[1]) if len(parts) > 1 else 0)
            except (ValueError, IndexError):
                pass

        return self.analyzer.analyze_code(code, file_name, target_version)

    def get_symbols(self, code: str, file_name: Optional[str] = None,
                    filter: Optional[str] = None) -> Dict[str, Any]:
        """
        Extract all symbols (classes, functions, variables) from Python code
        
        Args:
            code: Python code to analyze
            file_name: Optional filename for context
            filter: Optional filter ('class', 'function', 'variable', or 'all')
        
        Returns:
            List of symbols with their information
        """
        file_name = file_name or "temp.py"
        filter_kind = filter if filter and filter != 'all' else None

        return self.analyzer.get_symbols(code, file_name, filter_kind)

    def format_code(self, code: str) -> Dict[str, Any]:
        """
        Format Python code using black formatter
        
        Args:
            code: Python code to format
        
        Returns:
            Formatted code
        """
        return self.analyzer.format_code(code)

    def calculate_metrics(self, code: str, file_name: Optional[str] = None) -> Dict[str, Any]:
        """
        Calculate code metrics (complexity, maintainability, etc.)
        
        Args:
            code: Python code to analyze
            file_name: Optional filename for context
        
        Returns:
            Code metrics
        """
        file_name = file_name or "temp.py"
        return self.analyzer.calculate_metrics(code, file_name)

    def type_check(self, code: str, file_name: Optional[str] = None) -> Dict[str, Any]:
        """
        Run static type checking using mypy
        
        Args:
            code: Python code to type check
            file_name: Optional filename for context
        
        Returns:
            Type checking results with errors and warnings
        """
        file_name = file_name or "temp.py"
        return self.analyzer.type_check(code, file_name)

    def detect_dead_code(self, code: str, file_name: Optional[str] = None) -> Dict[str, Any]:
        """
        Detect unused functions, classes, and variables using vulture
        
        Args:
            code: Python code to analyze
            file_name: Optional filename for context
        
        Returns:
            List of unused code items
        """
        file_name = file_name or "temp.py"
        return self.analyzer.detect_dead_code(code, file_name)

    def comprehensive_lint(self, code: str, file_name: Optional[str] = None) -> Dict[str, Any]:
        """
        Run comprehensive linting using pylint
        
        Args:
            code: Python code to analyze
            file_name: Optional filename for context
        
        Returns:
            Comprehensive linting results
        """
        file_name = file_name or "temp.py"
        return self.analyzer.comprehensive_lint(code, file_name)

    def get_completions(self, code: str, line: int, column: int) -> Dict[str, Any]:
        """
        Get code completions at a specific position using jedi
        
        Args:
            code: Python code
            line: Line number (1-based)
            column: Column number (0-based)
        
        Returns:
            List of completion suggestions
        """
        return self.analyzer.get_completions(code, line, column)

    def format_with_autopep8(self, code: str, max_line_length: Optional[int] = None) -> Dict[str, Any]:
        """
        Format Python code using autopep8
        
        Args:
            code: Python code to format
            max_line_length: Maximum line length (default: 79)
        
        Returns:
            Formatted code
        """
        max_line_length = max_line_length or 79
        return self.analyzer.format_with_autopep8(code, max_line_length)