"""Code metrics model"""
from dataclasses import dataclass


@dataclass
class CodeMetrics:
    """Code quality metrics"""
    lines_of_code: int
    comment_lines: int
    blank_lines: int
    total_lines: int
    cyclomatic_complexity: int
    maintainability_index: float
    function_count: int
    class_count: int
    average_complexity: float = 0.0
