"""Python code analyzer with version awareness"""
import ast
import re
import sys
import tempfile
import os
from typing import List, Dict, Tuple, Optional
from io import StringIO
from contextlib import redirect_stdout, redirect_stderr

try:
    import black
    from black import Mode as BlackMode
    HAS_BLACK = True
except ImportError:
    HAS_BLACK = False

try:
    from radon.complexity import cc_visit
    from radon.metrics import mi_visit
    from radon.raw import analyze
    HAS_RADON = True
except ImportError:
    HAS_RADON = False

try:
    import pyflakes.api
    import pyflakes.reporter
    HAS_PYFLAKES = True
except ImportError:
    HAS_PYFLAKES = False

try:
    from mypy import api as mypy_api
    HAS_MYPY = True
except ImportError:
    HAS_MYPY = False

try:
    import vulture
    HAS_VULTURE = True
except ImportError:
    HAS_VULTURE = False

try:
    from pylint.lint import Run as PylintRun
    from pylint.reporters.text import TextReporter
    HAS_PYLINT = True
except ImportError:
    HAS_PYLINT = False

try:
    import jedi
    HAS_JEDI = True
except ImportError:
    HAS_JEDI = False

try:
    import autopep8
    HAS_AUTOPEP8 = True
except ImportError:
    HAS_AUTOPEP8 = False

from models import DiagnosticInfo, SymbolInfo, CodeMetrics


class PythonAnalyzer:
    """Analyzer for Python code with version awareness"""

    PYTHON_FEATURES = {
        (3, 12): ['type parameter syntax', 'f-string improvements'],
        (3, 11): ['exception groups', 'tomllib'],
        (3, 10): ['match/case', 'union types with |'],
        (3, 9): ['dict merge |', 'str removeprefix/removesuffix'],
        (3, 8): ['walrus operator :=', 'positional-only parameters /'],
        (3, 7): ['dataclasses', 'postponed annotations'],
        (3, 6): ['f-strings', 'variable annotations'],
        (3, 5): ['async/await', 'type hints'],
    }

    def __init__(self):
        self.current_version = sys.version_info[:2]

    def detect_python_version(self, code: str) -> Tuple[int, int]:
        """Detect target Python version from code"""

        # Check for shebang or comments
        lines = code.split('\n')[:10]
        for line in lines:
            # #!/usr/bin/env python3.10
            if line.startswith('#!') and 'python' in line:
                match = re.search(r'python(\d+)\.?(\d+)?', line)
                if match:
                    major = int(match.group(1))
                    minor = int(match.group(2)) if match.group(2) else 0
                    return (major, minor)

            # # requires: python>=3.8
            if 'requires' in line.lower() and 'python' in line.lower():
                match = re.search(r'python[>=<]+(\d+)\.(\d+)', line, re.IGNORECASE)
                if match:
                    return (int(match.group(1)), int(match.group(2)))

        # Detect from syntax features
        version = self._detect_from_syntax(code)
        if version:
            return version

        # Default to current runtime
        return self.current_version

    def _detect_from_syntax(self, code: str) -> Optional[Tuple[int, int]]:
        """Detect minimum Python version from syntax features"""

        # Python 3.10+ features
        if re.search(r'\bmatch\s+\w+:', code):
            return (3, 10)
        if ' | ' in code and 'Union' not in code:  # Union type syntax
            return (3, 10)

        # Python 3.8+ features
        if ':=' in code:  # Walrus operator
            return (3, 8)

        # Python 3.6+ features
        if re.search(r'f["\']', code):  # f-strings
            return (3, 6)

        # Python 3.5+ features
        if 'async def' in code or 'await ' in code:
            return (3, 5)

        # Python 3.0+
        if 'print(' in code:
            return (3, 0)

        # Python 2 indicators
        if re.search(r'\bprint\s+[^(]', code):
            return (2, 7)

        return None

    def analyze_code(self, code: str, file_name: str = "temp.py",
                     target_version: Optional[Tuple[int, int]] = None) -> Dict:
        """Analyze Python code for errors and issues"""

        if target_version is None:
            target_version = self.detect_python_version(code)

        diagnostics: List[DiagnosticInfo] = []

        # Syntax check with ast
        try:
            tree = ast.parse(code, filename=file_name)
        except SyntaxError as e:
            diagnostics.append(DiagnosticInfo(
                message=str(e.msg),
                category="SyntaxError",
                code="E0001",
                file=file_name,
                line=e.lineno or 0,
                column=e.offset or 0,
                severity="error"
            ))
            return {
                'success': False,
                'detected_version': f'{target_version[0]}.{target_version[1]}',
                'diagnostics': [self._diagnostic_to_dict(d) for d in diagnostics],
                'error_count': 1,
                'warning_count': 0
            }

        # Feature compatibility check
        feature_warnings = self._check_feature_compatibility(code, target_version)
        diagnostics.extend(feature_warnings)

        # Static analysis with pyflakes
        if HAS_PYFLAKES:
            pyflakes_diagnostics = self._run_pyflakes(code, file_name)
            diagnostics.extend(pyflakes_diagnostics)

        # Count by severity
        error_count = sum(1 for d in diagnostics if d.severity == 'error')
        warning_count = sum(1 for d in diagnostics if d.severity == 'warning')

        return {
            'success': error_count == 0,
            'detected_version': f'{target_version[0]}.{target_version[1]}',
            'diagnostics': [self._diagnostic_to_dict(d) for d in diagnostics],
            'error_count': error_count,
            'warning_count': warning_count
        }

    def _check_feature_compatibility(self, code: str,
                                     target_version: Tuple[int, int]) -> List[DiagnosticInfo]:
        """Check if code uses features not available in target version"""
        warnings = []

        # Check for match/case (3.10+)
        if target_version < (3, 10) and re.search(r'\bmatch\s+\w+:', code):
            warnings.append(DiagnosticInfo(
                message=f'match/case requires Python 3.10+, target is {target_version[0]}.{target_version[1]}',
                category="CompatibilityWarning",
                code="W9010",
                severity="warning"
            ))

        # Check for walrus operator (3.8+)
        if target_version < (3, 8) and ':=' in code:
            warnings.append(DiagnosticInfo(
                message=f'Walrus operator (:=) requires Python 3.8+, target is {target_version[0]}.{target_version[1]}',
                category="CompatibilityWarning",
                code="W9008",
                severity="warning"
            ))

        # Check for f-strings (3.6+)
        if target_version < (3, 6) and re.search(r'f["\']', code):
            warnings.append(DiagnosticInfo(
                message=f'f-strings require Python 3.6+, target is {target_version[0]}.{target_version[1]}',
                category="CompatibilityWarning",
                code="W9006",
                severity="warning"
            ))

        return warnings

    def _run_pyflakes(self, code: str, file_name: str) -> List[DiagnosticInfo]:
        """Run pyflakes static analysis"""
        diagnostics = []

        try:
            # Capture pyflakes output
            warning_stream = StringIO()
            reporter = pyflakes.reporter.Reporter(warning_stream, warning_stream)
            pyflakes.api.check(code, file_name, reporter=reporter)

            # Parse pyflakes output
            output = warning_stream.getvalue()
            for line in output.strip().split('\n'):
                if not line:
                    continue

                # Parse format: "filename:line:col: message"
                match = re.match(r'^(.+?):(\d+):(\d+): (.+)$', line)
                if match:
                    _, line_no, col_no, message = match.groups()

                    # Determine severity
                    severity = 'warning'
                    if 'undefined name' in message.lower() or 'imported but unused' in message.lower():
                        severity = 'error' if 'undefined' in message.lower() else 'warning'

                    diagnostics.append(DiagnosticInfo(
                        message=message,
                        category="PyflakesWarning",
                        code="F0001",
                        file=file_name,
                        line=int(line_no),
                        column=int(col_no),
                        severity=severity
                    ))
        except Exception:
            pass  # Silently fail if pyflakes has issues

        return diagnostics

    def get_symbols(self, code: str, file_name: str = "temp.py",
                    filter_kind: Optional[str] = None) -> Dict:
        """Extract all symbols from Python code"""

        try:
            tree = ast.parse(code, filename=file_name)
        except SyntaxError:
            return {
                'success': False,
                'error': 'Syntax error in code',
                'symbols': [],
                'count': 0
            }

        symbols: List[SymbolInfo] = []

        for node in ast.walk(tree):
            symbol = self._extract_symbol(node, code)
            if symbol and (filter_kind is None or filter_kind == 'all' or symbol.kind == filter_kind):
                symbols.append(symbol)

        return {
            'success': True,
            'symbols': [self._symbol_to_dict(s) for s in symbols],
            'count': len(symbols)
        }

    def _extract_symbol(self, node: ast.AST, code: str) -> Optional[SymbolInfo]:
        """Extract symbol information from AST node"""

        if isinstance(node, ast.ClassDef):
            decorators = [ast.unparse(d) for d in node.decorator_list] if hasattr(ast, 'unparse') else []
            return SymbolInfo(
                name=node.name,
                kind='class',
                line=node.lineno,
                column=node.col_offset,
                decorators=decorators if decorators else None
            )

        elif isinstance(node, ast.FunctionDef) or isinstance(node, ast.AsyncFunctionDef):
            decorators = [ast.unparse(d) for d in node.decorator_list] if hasattr(ast, 'unparse') else []
            type_annotation = None
            if node.returns:
                type_annotation = ast.unparse(node.returns) if hasattr(ast, 'unparse') else None

            return SymbolInfo(
                name=node.name,
                kind='function',
                line=node.lineno,
                column=node.col_offset,
                type_annotation=type_annotation,
                is_async=isinstance(node, ast.AsyncFunctionDef),
                decorators=decorators if decorators else None
            )

        elif isinstance(node, (ast.AnnAssign, ast.Assign)):
            # Variable assignments
            if isinstance(node, ast.AnnAssign) and isinstance(node.target, ast.Name):
                type_annotation = ast.unparse(node.annotation) if hasattr(ast, 'unparse') else None
                return SymbolInfo(
                    name=node.target.id,
                    kind='variable',
                    line=node.lineno,
                    column=node.col_offset,
                    type_annotation=type_annotation
                )

        return None

    def format_code(self, code: str) -> Dict:
        """Format Python code using black"""

        if not HAS_BLACK:
            return {
                'success': False,
                'error': 'black is not installed',
                'formatted_code': code
            }

        try:
            formatted = black.format_str(code, mode=BlackMode())
            return {
                'success': True,
                'formatted_code': formatted
            }
        except Exception as e:
            return {
                'success': False,
                'error': str(e),
                'formatted_code': code
            }

    def calculate_metrics(self, code: str, file_name: str = "temp.py") -> Dict:
        """Calculate code metrics"""

        if not HAS_RADON:
            return {
                'success': False,
                'error': 'radon is not installed'
            }

        try:
            # Parse for structure
            tree = ast.parse(code, filename=file_name)

            # Count classes and functions
            class_count = sum(1 for node in ast.walk(tree) if isinstance(node, ast.ClassDef))
            function_count = sum(1 for node in ast.walk(tree)
                                 if isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef)))

            # Calculate complexity
            complexity_results = cc_visit(code)
            total_complexity = sum(item.complexity for item in complexity_results)
            avg_complexity = total_complexity / len(complexity_results) if complexity_results else 0.0

            # Calculate maintainability index
            mi = mi_visit(code, multi=True)

            # Analyze raw metrics
            raw_metrics = analyze(code)

            metrics = CodeMetrics(
                lines_of_code=raw_metrics.loc,
                comment_lines=raw_metrics.comments,
                blank_lines=raw_metrics.blank,
                total_lines=raw_metrics.loc + raw_metrics.comments + raw_metrics.blank,
                cyclomatic_complexity=total_complexity,
                maintainability_index=mi,
                function_count=function_count,
                class_count=class_count,
                average_complexity=avg_complexity
            )

            return {
                'success': True,
                'metrics': self._metrics_to_dict(metrics)
            }
        except Exception as e:
            return {
                'success': False,
                'error': str(e)
            }

    def type_check(self, code: str, file_name: str = "temp.py") -> Dict:
        """
        Run static type checking using mypy
        
        Args:
            code: Python code to type check
            file_name: Optional filename for context
        
        Returns:
            Type checking results with errors and warnings
        """
        if not HAS_MYPY:
            return {
                'success': False,
                'error': 'mypy is not installed'
            }

        try:
            # Write code to temp file (mypy needs files)
            with tempfile.NamedTemporaryFile(mode='w', suffix='.py', delete=False) as f:
                f.write(code)
                temp_path = f.name

            try:
                # Run mypy
                result = mypy_api.run([temp_path, '--show-column-numbers', '--no-error-summary'])
                stdout, stderr, exit_code = result

                diagnostics = []

                # Parse mypy output
                for line in stdout.strip().split('\n'):
                    if not line or ':' not in line:
                        continue

                    # Parse format: "file:line:col: severity: message"
                    parts = line.split(':', 4)
                    if len(parts) >= 4:
                        try:
                            line_no = int(parts[1])
                            col_no = int(parts[2]) if parts[2].strip().isdigit() else 0
                            message = parts[3].strip() if len(parts) > 3 else line

                            # Determine severity
                            severity = 'error' if 'error' in message.lower() else 'warning'

                            diagnostics.append({
                                'message': message,
                                'category': 'TypeCheck',
                                'code': 'MYPY',
                                'file': file_name,
                                'line': line_no,
                                'column': col_no,
                                'severity': severity
                            })
                        except (ValueError, IndexError):
                            continue

                error_count = sum(1 for d in diagnostics if d['severity'] == 'error')
                warning_count = sum(1 for d in diagnostics if d['severity'] == 'warning')

                return {
                    'success': exit_code == 0,
                    'diagnostics': diagnostics,
                    'error_count': error_count,
                    'warning_count': warning_count
                }
            finally:
                # Cleanup temp file
                if os.path.exists(temp_path):
                    os.unlink(temp_path)

        except Exception as e:
            return {
                'success': False,
                'error': str(e)
            }

    def detect_dead_code(self, code: str, file_name: str = "temp.py") -> Dict:
        """
        Detect unused functions, classes, variables using vulture
        
        Args:
            code: Python code to analyze
            file_name: Optional filename for context
        
        Returns:
            List of unused code items
        """
        if not HAS_VULTURE:
            return {
                'success': False,
                'error': 'vulture is not installed'
            }

        try:
            # Write code to temp file
            with tempfile.NamedTemporaryFile(mode='w', suffix='.py', delete=False) as f:
                f.write(code)
                temp_path = f.name

            try:
                # Run vulture
                vuln = vulture.Vulture()
                vuln.scavenge([temp_path])

                unused_items = []
                for item in vuln.get_unused_code():
                    unused_items.append({
                        'name': item.name if hasattr(item, 'name') else 'unknown',
                        'type': item.typ if hasattr(item, 'typ') else 'unknown',
                        'line': item.first_lineno if hasattr(item, 'first_lineno') else 0,
                        'confidence': item.confidence if hasattr(item, 'confidence') else 60,
                        'message': f"Unused {item.typ}: {item.name}" if hasattr(item, 'typ') and hasattr(item, 'name') else str(item)
                    })

                return {
                    'success': True,
                    'unused_code': unused_items,
                    'count': len(unused_items)
                }
            finally:
                # Cleanup temp file
                if os.path.exists(temp_path):
                    os.unlink(temp_path)

        except Exception as e:
            return {
                'success': False,
                'error': str(e)
            }

    def comprehensive_lint(self, code: str, file_name: str = "temp.py") -> Dict:
        """
        Run comprehensive linting using pylint (modern API for pylint 3.x)
        
        Args:
            code: Python code to analyze
            file_name: Optional filename for context
        
        Returns:
            Comprehensive linting results
        """
        if not HAS_PYLINT:
            return {
                'success': False,
                'error': 'pylint is not installed'
            }

        try:
            # Write code to temp file
            with tempfile.NamedTemporaryFile(mode='w', suffix='.py', delete=False) as f:
                f.write(code)
                temp_path = f.name

            try:
                # Capture pylint output
                output_stream = StringIO()
                reporter = TextReporter(output_stream)

                # Run pylint with the modern API
                # Use exit=False to prevent system exit
                pylint_argv = [temp_path, '--output-format=text', '--reports=no']

                try:
                    # pylint.lint.Run modifies sys.argv, so we need to handle this carefully
                    PylintRun(pylint_argv, reporter=reporter, exit=False)
                except SystemExit:
                    # Pylint might still try to exit despite exit=False in some versions
                    pass

                output = output_stream.getvalue()
                diagnostics = []

                # Parse pylint output format: "path/file.py:line:col: code: message (symbol-name)"
                for line in output.split('\n'):
                    if not line or not ':' in line:
                        continue

                    # Try to match the standard pylint output format
                    # Format: file.py:10:0: C0116: Missing function or method docstring (missing-function-docstring)
                    match = re.match(r'^.+?:(\d+):(\d+): ([WCEFIR]\d+): (.+?)(?:\s+\([^)]+\))?$', line.strip())
                    if match:
                        line_no, col_no, code, message = match.groups()

                        # Determine severity from code prefix
                        # C=convention, R=refactor, W=warning, E=error, F=fatal, I=info
                        severity = 'error' if code[0] in ['E', 'F'] else 'warning'

                        diagnostics.append({
                            'message': message,
                            'category': 'Pylint',
                            'code': code,
                            'file': file_name,
                            'line': int(line_no),
                            'column': int(col_no),
                            'severity': severity
                        })

                error_count = sum(1 for d in diagnostics if d['severity'] == 'error')
                warning_count = sum(1 for d in diagnostics if d['severity'] == 'warning')

                return {
                    'success': error_count == 0,
                    'diagnostics': diagnostics,
                    'error_count': error_count,
                    'warning_count': warning_count
                }
            finally:
                # Cleanup temp file
                if os.path.exists(temp_path):
                    os.unlink(temp_path)

        except Exception as e:
            return {
                'success': False,
                'error': str(e)
            }

    def get_completions(self, code: str, line: int, column: int) -> Dict:
        """
        Get code completions at a specific position using jedi
        
        Args:
            code: Python code
            line: Line number (1-based)
            column: Column number (0-based)
        
        Returns:
            List of completion suggestions
        """
        if not HAS_JEDI:
            return {
                'success': False,
                'error': 'jedi is not installed'
            }

        try:
            script = jedi.Script(code)
            completions = script.complete(line, column)

            suggestions = []
            for comp in completions[:50]:  # Limit to 50 suggestions
                suggestions.append({
                    'name': comp.name,
                    'type': comp.type,
                    'description': comp.description if hasattr(comp, 'description') else None,
                    'signature': str(comp.get_signatures()[0]) if comp.get_signatures() else None
                })

            return {
                'success': True,
                'completions': suggestions,
                'count': len(suggestions)
            }

        except Exception as e:
            return {
                'success': False,
                'error': str(e)
            }

    def format_with_autopep8(self, code: str, max_line_length: int = 79) -> Dict:
        """
        Format Python code using autopep8
        
        Args:
            code: Python code to format
            max_line_length: Maximum line length
        
        Returns:
            Formatted code
        """
        if not HAS_AUTOPEP8:
            return {
                'success': False,
                'error': 'autopep8 is not installed',
                'formatted_code': code
            }

        try:
            formatted = autopep8.fix_code(code, options={'max_line_length': max_line_length})
            return {
                'success': True,
                'formatted_code': formatted
            }
        except Exception as e:
            return {
                'success': False,
                'error': str(e),
                'formatted_code': code
            }

    def _diagnostic_to_dict(self, diagnostic: DiagnosticInfo) -> Dict:
        """Convert DiagnosticInfo to dictionary"""
        return {
            'message': diagnostic.message,
            'category': diagnostic.category,
            'code': diagnostic.code,
            'file': diagnostic.file,
            'line': diagnostic.line,
            'column': diagnostic.column,
            'severity': diagnostic.severity
        }

    def _symbol_to_dict(self, symbol: SymbolInfo) -> Dict:
        """Convert SymbolInfo to dictionary"""
        return {
            'name': symbol.name,
            'kind': symbol.kind,
            'line': symbol.line,
            'column': symbol.column,
            'type_annotation': symbol.type_annotation,
            'container_name': symbol.container_name,
            'is_async': symbol.is_async,
            'decorators': symbol.decorators
        }

    def _metrics_to_dict(self, metrics: CodeMetrics) -> Dict:
        """Convert CodeMetrics to dictionary"""
        return {
            'lines_of_code': metrics.lines_of_code,
            'comment_lines': metrics.comment_lines,
            'blank_lines': metrics.blank_lines,
            'total_lines': metrics.total_lines,
            'cyclomatic_complexity': metrics.cyclomatic_complexity,
            'maintainability_index': metrics.maintainability_index,
            'function_count': metrics.function_count,
            'class_count': metrics.class_count,
            'average_complexity': metrics.average_complexity
        }