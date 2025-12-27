"""LabKey MCP Tools package.

This package contains domain-specific tool modules for the LabKey MCP server.
Each module exports a `register_tools(mcp)` function to register its tools.

Modules:
- common: Shared utilities + list_queries (for proposing schema docs)
- exceptions: Exception triage tools
- nightly: Nightly test analysis tools
- wiki: Wiki page tools
- support: Support board tools
- attachments: Attachment handling tools
- issues: Issue tracking tools
"""

from . import common
from . import exceptions
from . import nightly
from . import wiki
from . import support
from . import attachments
from . import issues


def register_all_tools(mcp):
    """Register all tools from all modules.

    Order: PRIMARY tools first, then DRILL-DOWN, then limited discovery.
    """
    # PRIMARY tools first (aggregate reports)
    nightly.register_tools(mcp)      # get_daily_test_summary
    exceptions.register_tools(mcp)   # save_exceptions_report
    support.register_tools(mcp)      # get_support_summary
    issues.register_tools(mcp)       # save_issues_report

    # DRILL-DOWN tools
    wiki.register_tools(mcp)
    attachments.register_tools(mcp)

    # Limited discovery (list_queries only - guides toward schema docs)
    common.register_tools(mcp)
