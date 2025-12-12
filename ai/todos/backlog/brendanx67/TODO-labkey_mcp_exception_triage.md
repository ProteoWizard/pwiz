# TODO-labkey_mcp_exception_triage.md

## Overview

Create an MCP server for authenticated LabKey/Panorama access, enabling Claude Code to analyze production exception logs from skyline.ms and propose fixes.

## Motivation

The Skyline exception tracking system at skyline.ms logs uncaught exceptions from users. Currently, reviewing these requires manual effort to:
- Identify patterns (same stack trace = same bug)
- Filter noise (old versions, same user hitting same issue repeatedly)
- Prioritize by impact (frequency, affected users, severity)
- Trace to root cause in codebase

An LLM agent with access to this data could automate triage and even propose fixes.

## Goals

1. **Authenticated LabKey access** - MCP server that handles basic auth + CSRF tokens
2. **Exception analysis** - Query and summarize exception patterns
3. **Code correlation** - Match stack traces to current codebase
4. **Fix proposals** - Generate fixes for clear-cut issues
5. **PR automation** - Create PRs for approved fixes

## Phases

### Phase 1: MCP Server for LabKey Access

**Objective**: Read-only authenticated access to skyline.ms LabKey data

**Tasks**:
- [ ] Study MCP SDK (Python recommended for quick prototyping)
- [ ] Implement LabKey authentication flow (based on PanoramaClient/RequestHelper.cs)
  - Basic auth header
  - CSRF token retrieval from `/project/home/ensureLogin.view`
  - Cookie management
- [ ] Create initial tools:
  - `query_exceptions(days, folder)` - Fetch recent exceptions
  - `get_exception_details(id)` - Get full stack trace and context
  - `list_folders()` - Browse LabKey folder structure
- [ ] Configure MCP server in Claude Code (local scope for credentials)
- [ ] Test with simple queries

**Reference**: `pwiz_tools/Shared/PanoramaClient/RequestHelper.cs` lines 371-395 for CSRF flow

### Phase 2: Exception Analysis Workflows

**Objective**: Summarize and categorize exceptions intelligently

**Tasks**:
- [ ] Create analysis prompts/commands:
  - Daily exception summary
  - Group by stack trace signature
  - Filter by version (current vs old releases)
  - Identify repeat offenders (same user, same issue)
- [ ] Add tools for enriched queries:
  - `get_exception_frequency(stack_trace_hash)` - How often does this occur?
  - `get_affected_versions(exception_id)` - Which versions see this?
  - `get_user_impact(exception_id)` - Unique users affected
- [ ] Build `/pw-exceptions` slash command for daily triage workflow

### Phase 3: Code Correlation

**Objective**: Match exceptions to codebase and identify root causes

**Tasks**:
- [ ] Parse stack traces to extract:
  - File paths and line numbers
  - Method signatures
  - Exception types
- [ ] Cross-reference with current codebase:
  - Has the code changed since the exception?
  - Is the method still present?
  - What's the surrounding context?
- [ ] Identify patterns:
  - Null reference → missing null check
  - Index out of range → bounds validation
  - Network errors → retry/timeout handling

### Phase 4: Fix Proposals and PR Automation

**Objective**: Generate and submit fixes for clear-cut issues

**Tasks**:
- [ ] Create fix proposal workflow:
  - Analyze exception + code context
  - Generate candidate fix
  - Validate fix doesn't break existing tests
  - Create branch and PR
- [ ] Add safety guardrails:
  - Human approval required before PR creation
  - Confidence scoring (only propose high-confidence fixes)
  - Test verification before submission
- [ ] Track fix success rate for continuous improvement

## Technical Notes

### MCP Server Architecture

```
labkey-mcp-server.py
├── auth.py          # LabKey authentication (basic + CSRF)
├── client.py        # HTTP client with session management
├── tools/
│   ├── exceptions.py    # Exception query tools
│   ├── folders.py       # Folder navigation
│   └── analysis.py      # Aggregation/analysis tools
└── server.py        # MCP server entry point
```

### Credential Management

```bash
# Local scope - credentials stay on developer machine
claude mcp add --transport stdio labkey \
  --env LABKEY_URL=https://skyline.ms \
  --env LABKEY_USER=${LABKEY_USER} \
  --env LABKEY_API_KEY=${LABKEY_API_KEY} \
  --scope local \
  -- python labkey-mcp-server.py
```

### LabKey API Endpoints

Key endpoints for exception tracking (verify against actual skyline.ms schema):
- `query/selectRows.api` - Query exception tables
- `query/getSchemas.api` - Discover available schemas
- `project/getContainers.api` - List folders

## Resources

- [MCP SDK Documentation](https://modelcontextprotocol.io/quickstart/server)
- [MCP Example Servers](https://github.com/modelcontextprotocol/servers)
- [LabKey API Documentation](https://www.labkey.org/Documentation/wiki-page.view?name=remoteAPIs)
- Existing code: `pwiz_tools/Shared/PanoramaClient/` - Authentication patterns

## Success Criteria

- [ ] Can query exception data from skyline.ms via Claude Code
- [ ] Daily triage takes <5 minutes instead of 30+ minutes
- [ ] At least one bug fix PR generated from exception analysis
- [ ] False positive rate for fix proposals <20%

## Open Questions

1. What's the exact schema for exception data on skyline.ms?
2. Are there rate limits or access restrictions to consider?
3. Should fixes target current release branch or master?
4. How to handle exceptions that span multiple components?
