"""LabKey MCP Tools package.

This package contains domain-specific tool modules for the LabKey MCP server.
Each module exports a `register_tools(mcp)` function to register its tools.

Modules:
- common: Shared utilities and discovery tools
- exceptions: Exception triage tools
- nightly: Nightly test analysis tools
- wiki: Wiki page tools
- support: Support board tools
- attachments: Attachment handling tools
"""

from . import common
from . import exceptions
from . import nightly
from . import wiki
from . import support
from . import attachments


def register_all_tools(mcp):
    """Register all tools from all modules."""
    common.register_tools(mcp)
    exceptions.register_tools(mcp)
    nightly.register_tools(mcp)
    wiki.register_tools(mcp)
    support.register_tools(mcp)
    attachments.register_tools(mcp)
