# TODO-20251227_mcp_context_optimization.md

## Branch Information
- **Branch**: `ai-context`
- **Created**: 2025-12-27
- **Status**: ✅ Complete
- **Objective**: Reduce MCP context usage and prioritize high-level tools over discovery tools

Note: ai-context work is managed directly on the branch and merged to master weekly via sync PR. See ai/docs/ai-context-branch-strategy.md.

## Problem

Claude Code's `/doctor` command reports:
```
Context Usage Warnings
└ ‼ Large MCP tools context (~34,760 tokens > 25,000)
  └ MCP servers:
    └ labkey: 27 tools (~21,341 tokens)
    └ gmail: 19 tools (~13,419 tokens)
```

Additionally, Claude sessions often use low-level discovery tools (`query_table`, `list_schemas`) instead of the curated high-level tools (`get_daily_test_summary`) that encode domain knowledge and provide better results.

## Goals

1. **Reduce context tokens** - Cut LabKey MCP from ~21K to ~10K tokens
2. **Prioritize high-level tools** - Guide Claude to use curated tools first
3. **Discourage raw queries** - When high-level tools fall short, propose extensions rather than falling back to query_table

## Solution Approach

### Phase 1: Tool Hierarchy Documentation

Add clear guidance about tool usage priority:

**Preferred Tools (use first)**:
- `get_daily_test_summary` - Primary tool for nightly test review
- `save_exceptions_report` - Primary tool for exception review
- `get_support_summary` - Primary tool for support board review

**Drill-Down Tools (after preferred)**:
- `get_run_failures`, `get_run_leaks` - Details for specific runs
- `get_exception_details` - Full stack trace for specific exception
- `get_support_thread` - Full thread content

**Discovery Tools (rarely needed)**:
- `query_table`, `list_schemas`, `list_queries` - Raw LabKey access
- Only use if no higher-level tool exists
- Propose extensions to high-level tools instead

### Phase 2: Terse Docstrings

Cut docstring verbosity by ~60%. Example:

**Before** (~15 lines):
```python
"""Query all 6 nightly test folders and save a consolidated report for one day.

This is the primary tool for daily test review. It queries all folders
in one call and saves a full report to ai/.tmp/nightly-report-YYYYMMDD.md.

The nightly test "day" runs from 8:01 AM to 8:00 AM the next day.
So report_date="2025-12-17" queries runs from Dec 16 8:01 AM to Dec 17 8:00 AM.

Args:
    report_date: Date in YYYY-MM-DD format - the END of the nightly window
    server: LabKey server hostname (default: skyline.ms)

Returns:
    Brief summary with file path. Full details are in the saved file.
"""
```

**After** (~3 lines):
```python
"""PRIMARY: Daily nightly test report across all 6 folders. Saves to ai/.tmp/nightly-report-YYYYMMDD.md.

Args:
    report_date: Date YYYY-MM-DD (end of nightly window, 8AM-8AM boundary)
"""
```

### Phase 3: Tool Reordering

Register high-level tools first so they appear earlier in the tool list.

### Phase 4: Docstring Hints

Add to high-level tools: "**PRIMARY tool for X.**"
Add to low-level tools: "Low-level. Prefer get_daily_test_summary for nightly analysis."

## Tasks

- [x] Create tool hierarchy documentation in ai/docs/mcp/
- [x] Update LabKey tool docstrings to be terse
- [x] Add PRIMARY/DRILL-DOWN/DISCOVERY labels to docstrings
- [x] Reorder tool registration (high-level first)
- [x] Test with `/doctor` to verify token reduction
- [x] Test in session to verify tool selection behavior

## Key Files

- `ai/mcp/LabKeyMcp/tools/*.py` - Tool definitions
- `ai/mcp/LabKeyMcp/server.py` - Tool registration
- `ai/docs/mcp/development-guide.md` - MCP documentation

## Progress Log

### 2025-12-27 - Session 2 (final)
- **Verified with /doctor**: LabKey MCP reduced from ~21K to ~17K tokens (20% reduction)
- **Removed 3 DISCOVERY tools** - `list_schemas`, `list_containers`, `query_table` deleted from common.py
  - These led Claude down unproductive paths and risked dangerous queries
  - Only `list_queries` retained - for proposing schema documentation
- **Updated `list_queries`** to include guidance toward schema documentation workflow
- **Updated tool-hierarchy.md** to reflect removed tools and clearer anti-patterns
- Tool count reduced: 27 → 24 tools

### 2025-12-27 - Session 2
- **Created** `ai/docs/mcp/tool-hierarchy.md` documenting PRIMARY/DRILL-DOWN/DISCOVERY categories
- **Updated README.md** with reference to tool hierarchy docs
- **Updated all 27 tool docstrings** with terse descriptions and category labels:
  - PRIMARY tools: get_daily_test_summary, save_exceptions_report, get_support_summary, save_issues_report
  - DRILL-DOWN tools: Most tools (get_run_failures, get_exception_details, etc.)
  - DISCOVERY tools: list_schemas, list_queries, list_containers, query_table
- **Reordered tool registration** in `__init__.py`:
  - PRIMARY modules first (nightly, exceptions, support, issues)
  - DRILL-DOWN modules next (wiki, attachments)
  - DISCOVERY module last (common)
- Estimated token reduction: ~60% of docstring content removed
- **Enhanced tool-hierarchy.md** with "Proposing New Data Access" workflow:
  - Captured wisdom that DISCOVERY tools are problematic (50MB blobs, 700M row tables)
  - Documented the better approach: stub schema → human populates → server-side query → high-level tool
  - Referenced development-guide.md for implementation details

### 2025-12-27 - Session 1
- Identified issue from `/doctor` output: ~35K tokens for MCP tools
- LabKey contributes ~21K tokens (27 tools)
- Gmail contributes ~13K tokens (19 tools)
- Observed sessions using query_table instead of get_daily_test_summary
- Created this TODO to track optimization work
