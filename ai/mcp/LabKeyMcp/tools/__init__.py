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
- patterns: Pattern detection for daily reports
- computers: Computer status management (deactivate/reactivate)
- nightly_history: Historical tracking for failures, leaks, hangs

Internal utilities (no MCP tools):
- stacktrace: Stack trace normalization for pattern matching
"""

from . import common
from . import exceptions
from . import nightly
from . import wiki
from . import support
from . import attachments
from . import issues
from . import patterns
from . import computers
from . import nightly_history
from . import stacktrace  # Internal utility, no MCP tools


def register_all_tools(mcp):
    """Register all tools from all modules.

    Order: PRIMARY tools first, then DRILL-DOWN, then limited discovery.
    """
    # PRIMARY tools first (aggregate reports)
    nightly.register_tools(mcp)      # get_daily_test_summary
    exceptions.register_tools(mcp)   # save_exceptions_report
    support.register_tools(mcp)      # get_support_summary
    issues.register_tools(mcp)       # save_issues_report
    patterns.register_tools(mcp)     # analyze_daily_patterns, save_daily_summary
    nightly_history.register_tools(mcp)  # backfill_nightly_history, query_test_history

    # DRILL-DOWN tools
    wiki.register_tools(mcp)
    attachments.register_tools(mcp)
    computers.register_tools(mcp)    # deactivate_computer, reactivate_computer, etc.

    # Limited discovery (list_queries only - guides toward schema docs)
    common.register_tools(mcp)
