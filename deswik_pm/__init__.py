"""Deswik Process Map SDK.

Read, edit, and create Deswik process map files (*.ddf).
"""
from .ddf import DdfFile
from .tag import NodeTag, CommandEntry
from .diagram import Diagram, Node
from .builder import ProcessMapBuilder
from . import macros

__version__ = "0.1.0"
__all__ = [
    "DdfFile", "NodeTag", "CommandEntry", "Diagram", "Node",
    "ProcessMapBuilder", "macros",
]
