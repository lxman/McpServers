"""Symbol information model"""
from dataclasses import dataclass
from typing import Optional


@dataclass
class SymbolInfo:
    """Information about a code symbol (class, function, variable, etc.)"""
    name: str
    kind: str  # 'class', 'function', 'method', 'variable', 'import'
    line: int
    column: int
    type_annotation: Optional[str] = None
    container_name: Optional[str] = None
    is_async: bool = False
    decorators: Optional[list[str]] = None
