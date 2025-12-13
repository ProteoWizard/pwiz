# TODO-20251213_labkey_mcp_exception_triage.md

## Branch Information
- **Branch**: `Skyline/work/20251213_labkey_mcp_exception_triage`
- **Created**: 2025-12-13
- **Completed**: (pending)
- **Status**: 🚧 In Progress
- **PR**: (pending)
- **Objective**: Create MCP server for LabKey/Panorama access to analyze exception logs

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

1. **Authenticated LabKey access** - MCP server using official LabKey Python API
2. **Exception analysis** - Query and summarize exception patterns
3. **Code correlation** - Match stack traces to current codebase
4. **Fix proposals** - Generate fixes for clear-cut issues
5. **PR automation** - Create PRs for approved fixes

## Phases

### Phase 1: MCP Server for LabKey Access

**Objective**: Read-only authenticated access to skyline.ms LabKey data

**Approach**: Use official LabKey Python API (`pip install labkey`) instead of porting C# patterns.
The official API handles authentication, CSRF tokens, and session management internally.

**Tasks**:
- [x] Study MCP Python SDK (`mcp` package)
- [x] Create MCP server skeleton with dependencies (`mcp`, `labkey`)
- [x] Configure netrc authentication for skyline.ms
  - Created `claude.c.skyline@gmail.com` account
  - Added to "Agents" group with read access to `/home/issues/exceptions`
  - Configured `_netrc` with credentials
- [x] Create initial MCP tools wrapping LabKey API:
  - `query_exceptions(days, folder)` - Fetch recent exceptions
  - `get_exception_details(id)` - Get full stack trace and context
  - `list_containers()` - Browse LabKey folder structure
  - `list_schemas()` - Discover available schemas
  - `list_queries()` - List queries in a schema
  - `query_table()` - Generic table query
- [x] Configure MCP server in Claude Code (local scope)
- [ ] Test MCP tools from Claude Code (requires restart)

**Dependencies**:
- `mcp` - Model Context Protocol Python SDK
- `labkey` - Official LabKey Python API (handles auth internally)

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

### MCP Server Location

```
pwiz_tools/Skyline/Executables/DevTools/LabKeyMcp/
├── server.py        # MCP server with all tools
├── pyproject.toml   # Dependencies (mcp, labkey)
└── README.md        # Setup instructions
```

The official `labkey` package handles all authentication internally via netrc.

### Credential Management

**Step 1**: Create `_netrc` file in user home directory (Windows) or `.netrc` (Unix):
```
machine skyline.ms
login your-email@example.com
password your-password
```

**Step 2**: Register MCP server in Claude Code:
```bash
claude mcp add labkey -- python C:/proj/pwiz/pwiz_tools/Skyline/Executables/DevTools/LabKeyMcp/server.py
```

### Available Tools

| Tool | Description |
|------|-------------|
| `list_schemas` | List available schemas in a container |
| `list_queries` | List queries/tables in a schema |
| `list_containers` | List child folders in a container |
| `query_table` | Query data from any LabKey table |
| `query_exceptions` | Convenience wrapper for exception queries |
| `get_exception_details` | Get full details for a specific exception |

## Resources

- [MCP Python SDK](https://github.com/modelcontextprotocol/python-sdk)
- [MCP Server Quickstart](https://modelcontextprotocol.io/quickstart/server)
- [LabKey Python API](https://github.com/LabKey/labkey-api-python)
- [LabKey API Documentation](https://www.labkey.org/Documentation/wiki-page.view?name=python)

## Success Criteria

- [ ] Can query exception data from skyline.ms via Claude Code
- [ ] Daily triage takes <5 minutes instead of 30+ minutes
- [ ] At least one bug fix PR generated from exception analysis
- [ ] False positive rate for fix proposals <20%

## Open Questions

1. ~~What's the exact schema for exception data on skyline.ms?~~ **RESOLVED**: `announcement.Announcement` in `/home/issues/exceptions`
2. Are there rate limits or access restrictions to consider?
3. Should fixes target current release branch or master?
4. How to handle exceptions that span multiple components?

## Future Data Sources (discovered during this sprint)

### Nightly Test Results (`testresults` schema)

Another valuable data source on skyline.ms. Container path TBD.

**Tables available:**
| Table | Description |
|-------|-------------|
| `testruns` | Test run summaries (passedtests, failedtests, leakedtests, duration, githash) |
| `testfails` | Individual failed test details |
| `testpasses` | Individual passed test details |
| `memoryleaks` | Memory leak detections |
| `handleleaks` | Handle leak detections |
| `hangs` | Test hang detections |

**Key columns in `testresults.testruns`:**
- `id`, `duration`, `time`, `revision`
- `passedtests`, `failedtests`, `leakedtests`
- `averagemem`, `githash`, `flagged`
- `container`, `userid`, `timestamp`
- `xml`, `pointsummary`, `log`

Would enable Claude Code to:
- Review nightly test failures
- Identify flaky tests
- Track test trends over time
- Correlate failures with code changes (via githash)

*Separate documentation and MCP tools needed - not part of exception triage.*

### Gmail Integration

The `claude.c.skyline@gmail.com` account could receive:
- Exception report notifications
- Nightly test result summaries
- Other automated alerts

Requires Gmail MCP server setup (OAuth2).

## Progress Log

### 2025-12-13: Phase 1 Implementation
- Created MCP server at `pwiz_tools/Skyline/Executables/DevTools/LabKeyMcp/`
- Using official LabKey Python API (`labkey` package) - handles auth via netrc
- Created `claude.c.skyline@gmail.com` account for agent access
- Discovered schema: `announcement.Announcement` contains 12,837 exceptions
- Successfully queried 82 exceptions from last 30 days
- Registered MCP server with Claude Code (local scope)
- **Next**: Restart Claude Code and test MCP tool access
