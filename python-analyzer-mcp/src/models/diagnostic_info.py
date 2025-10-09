"""Data models for Python analyzer"""
from dataclasses import dataclass
from typing import Optional


@dataclass
class DiagnosticInfo:
    """Information about a diagnostic (error, warning, etc.)"""
    message: str
    category: str
    code: Optional[str] = None
    file: Optional[str] = None
    line: Optional[int] = None
    column: Optional[int] = None
    severity: str = "error"
