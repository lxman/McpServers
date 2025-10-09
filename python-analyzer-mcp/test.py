#!/usr/bin/env python3
"""Test script for Python Analyzer - Now with ALL features!"""
import sys
sys.path.insert(0, '.')

from src.tools import PythonTools

def test_analyzer():
    """Test all analyzer functions"""
    tools = PythonTools()

    # Test code with various issues
    test_code = """
# Test Python 3.10+ feature
def greet(name: str) -> str:
    match name:
        case "Alice":
            return "Hello, Alice!"
        case _:
            return f"Hello, {name}!"

x: int = "wrong type"  # Type annotation mismatch
print(undefined_var)  # Undefined variable

if (y := 10) > 5:  # Walrus operator (3.8+)
    print(y)
"""

    print("=" * 80)
    print("TEST 1: Analyze Code (Python 3.7 target)")
    print("=" * 80)
    result = tools.analyze_code(test_code, python_version="3.7")
    print(f"Detected Version: {result['detected_version']}")
    print(f"Success: {result['success']}")
    print(f"Errors: {result['error_count']}, Warnings: {result['warning_count']}")
    print("\nDiagnostics:")
    for diag in result['diagnostics'][:5]:  # Show first 5
        print(f"  [{diag['severity'].upper()}] Line {diag.get('line', '?')}: {diag['message']}")

    print("\n" + "=" * 80)
    print("TEST 2: Extract Symbols")
    print("=" * 80)
    symbol_result = tools.get_symbols(test_code)
    print(f"Success: {symbol_result['success']}")
    print(f"Found {symbol_result['count']} symbols:")
    for symbol in symbol_result['symbols']:
        type_info = f" -> {symbol['type_annotation']}" if symbol['type_annotation'] else ""
        print(f"  {symbol['kind']}: {symbol['name']}{type_info} (line {symbol['line']})")

    print("\n" + "=" * 80)
    print("TEST 3: Format Code")
    print("=" * 80)
    messy_code = "def test(  x,y  ):return x+y"
    format_result = tools.format_code(messy_code)
    print(f"Success: {format_result['success']}")
    if format_result['success']:
        print("Original:", messy_code)
        print("Formatted:")
        print(format_result['formatted_code'])

    print("\n" + "=" * 80)
    print("TEST 4: Calculate Metrics")
    print("=" * 80)
    metrics_code = """
def complex_function(x):
    if x > 0:
        for i in range(x):
            if i % 2 == 0:
                print(i)
    return x
"""
    metrics_result = tools.calculate_metrics(metrics_code)
    print(f"Success: {metrics_result['success']}")
    if metrics_result['success']:
        metrics = metrics_result['metrics']
        print(f"  Lines of Code: {metrics['lines_of_code']}")
        print(f"  Cyclomatic Complexity: {metrics['cyclomatic_complexity']}")
        print(f"  Maintainability Index: {metrics['maintainability_index']:.1f}")
        print(f"  Function Count: {metrics['function_count']}")
        print(f"  Average Complexity: {metrics['average_complexity']:.1f}")

    print("\n" + "=" * 80)
    print("TEST 5: Type Checking ⭐ NEW")
    print("=" * 80)
    type_check_code = """
def greet(name: str) -> int:
    return "Hello, " + name  # Type error: returns str not int

def add(a: int, b: int) -> int:
    return a + b

result: str = add(1, 2)  # Type error: int assigned to str
"""
    type_result = tools.type_check(type_check_code)
    print(f"Success: {type_result['success']}")
    if 'error' in type_result:
        print(f"Note: {type_result['error']}")
    elif 'diagnostics' in type_result:
        print(f"Found {type_result['error_count']} type errors:")
        for diag in type_result['diagnostics'][:3]:
            print(f"  Line {diag['line']}: {diag['message']}")

    print("\n" + "=" * 80)
    print("TEST 6: Dead Code Detection ⭐ NEW")
    print("=" * 80)
    dead_code = """
def used_function():
    return "I'm used!"

def unused_function():
    return "Nobody calls me"

def another_unused():
    x = 10
    y = 20
    return x + y

class UnusedClass:
    def method(self):
        pass

result = used_function()
"""
    dead_result = tools.detect_dead_code(dead_code)
    print(f"Success: {dead_result['success']}")
    if 'error' in dead_result:
        print(f"Note: {dead_result['error']}")
    elif 'unused_code' in dead_result:
        print(f"Found {dead_result['count']} unused items:")
        for item in dead_result['unused_code']:
            print(f"  {item['type']}: {item['name']} (line {item['line']}, confidence: {item['confidence']}%)")

    print("\n" + "=" * 80)
    print("TEST 7: Comprehensive Lint ⭐ NEW")
    print("=" * 80)
    lint_code = """
def MyFunction(x):
    y=x+1
    return y

def another_function():
    pass
"""
    lint_result = tools.comprehensive_lint(lint_code)
    print(f"Success: {lint_result['success']}")
    if 'error' in lint_result:
        print(f"Note: {lint_result['error']}")
    elif 'diagnostics' in lint_result:
        print(f"Found {lint_result['warning_count']} warnings:")
        for diag in lint_result['diagnostics'][:5]:
            print(f"  [{diag['code']}] Line {diag['line']}: {diag['message']}")

    print("\n" + "=" * 80)
    print("TEST 8: Code Completion ⭐ NEW")
    print("=" * 80)
    completion_code = """import os
os."""
    completion_result = tools.get_completions(completion_code, line=2, column=3)
    print(f"Success: {completion_result['success']}")
    if 'error' in completion_result:
        print(f"Note: {completion_result['error']}")
    elif 'completions' in completion_result:
        print(f"Found {completion_result['count']} completions (showing first 10):")
        for comp in completion_result['completions'][:10]:
            sig = f" {comp['signature']}" if comp['signature'] else ""
            print(f"  {comp['name']} ({comp['type']}){sig}")

    print("\n" + "=" * 80)
    print("TEST 9: Format with autopep8 ⭐ NEW")
    print("=" * 80)
    autopep8_code = "def test(  x,y,z  ):x=1;y=2;z=3;return x+y+z"
    autopep8_result = tools.format_with_autopep8(autopep8_code, max_line_length=79)
    print(f"Success: {autopep8_result['success']}")
    if 'error' in autopep8_result:
        print(f"Note: {autopep8_result['error']}")
    elif autopep8_result['success']:
        print("Original:", autopep8_code)
        print("Formatted:")
        print(autopep8_result['formatted_code'])

    print("\n" + "=" * 80)
    print("🎉 All tests completed!")
    print("=" * 80)
    print("\n📊 Feature Summary:")
    print("  ✅ Code Analysis (pyflakes)")
    print("  ✅ Symbol Extraction (AST)")
    print("  ✅ Code Formatting (black)")
    print("  ✅ Code Metrics (radon)")
    print("  ⭐ Type Checking (mypy)")
    print("  ⭐ Dead Code Detection (vulture)")
    print("  ⭐ Comprehensive Linting (pylint)")
    print("  ⭐ Code Completion (jedi)")
    print("  ⭐ Alternative Formatting (autopep8)")
    print("\n💡 Note: Features marked with ⭐ require additional dependencies.")
    print("   Install all dependencies with: pip install -r requirements.txt")

if __name__ == "__main__":
    test_analyzer()