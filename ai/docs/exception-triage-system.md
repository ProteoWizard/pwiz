# Skyline Exception Triage System

This document describes the system for querying and analyzing Skyline exception reports from skyline.ms using Claude Code.

## Overview

Skyline users can submit exception reports when the software crashes. These reports are stored on the skyline.ms LabKey server and contain:
- Exception type and stack trace
- Skyline version and installation ID
- User email and comments (optional)
- Timestamp

Claude Code can query this data via an MCP server to assist with exception triage.

## Architecture

```
Claude Code
    │
    └── MCP Protocol (stdio)
            │
            └── LabKeyMcp Server (Python)
                    │
                    └── labkey Python API
                            │
                            └── skyline.ms LabKey Server
                                    │
                                    └── announcement.Announcement table
                                        (12,837+ exception reports)
```

## Components

### MCP Server

**Location**: `ai/mcp/LabKeyMcp/`

| File | Purpose |
|------|---------|
| `server.py` | MCP server entry point |
| `tools/` | Tool modules by domain |
| `tools/exceptions.py` | Exception triage tools |
| `tools/common.py` | Shared utilities and discovery tools |
| `queries/` | Server-side query documentation |
| `pyproject.toml` | Python dependencies |
| `test_connection.py` | Standalone connection test |
| `README.md` | Setup instructions |

### Available MCP Tools

| Tool | Description |
|------|-------------|
| `save_exceptions_report(report_date)` | Generate daily report, save to `ai/.tmp/exceptions-report-YYYYMMDD.md` |
| `query_exceptions(days, max_rows)` | Query recent exceptions, returns summary |
| `get_exception_details(exception_id)` | Get full stack trace and details for one exception |
| `list_schemas(container_path)` | Discover available schemas |
| `list_queries(schema_name)` | List tables in a schema |
| `list_containers(parent_path)` | Browse folder structure |
| `query_table(schema, query, ...)` | Generic table query |

### Authentication

Each developer uses a personal `+claude` account for MCP access:
- **Team members**: `yourname+claude@proteinms.net`
- **Interns/others**: `yourname+claude@gmail.com`
- **Group**: "Agents" on skyline.ms
- **Permissions**: Read-only access to most containers, edit access to wiki pages

> **Why individual accounts?** Using `+claude` suffix accounts provides:
> - **Attribution**: Edits are tracked to the specific developer's Claude instance
> - **Least privilege**: The Agents group has minimal permissions (read-only for most data)
> - **Gmail trick**: Emails to `user+tag@gmail.com` go to `user@gmail.com`, so notifications still reach you
>
> **Important**: The `+claude` suffix only works with Gmail-backed email providers (@proteinms.net, @gmail.com). It will **not** work with @uw.edu or similar providers.

Credentials are stored in the user's netrc file (not in the repository):
- **Unix/macOS**: `~/.netrc`
- **Windows**: `~/.netrc` or `~/_netrc`

## Data Schema

Exception data lives at:
- **Server**: `skyline.ms`
- **Container**: `/home/issues/exceptions`
- **Schema**: `announcement`
- **Query**: `Announcement`

### Key Columns

| Column | Description |
|--------|-------------|
| `RowId` | Unique exception identifier |
| `Title` | Exception summary (type, location, version, install ID) |
| `FormattedBody` | Full report with stack trace |
| `Created` | When the exception was reported |
| `Modified` | Last modification time |
| `Status` | Triage status (if assigned) |
| `AssignedTo` | Developer assigned to fix |

### Title Format

Exception titles follow a pattern:
```
ExceptionType | FileName.cs:line N | Version | InstallationIdSuffix
```

Example:
```
TargetInvocationException | RetentionTimeValues.cs:line 98 | 24.1.0.199-6a0775ef83 | 25fc6cffcaa1
```

## Setup

### Prerequisites

1. Python 3.10+
2. Access to skyline.ms (team member)

### Installation

```bash
# Install dependencies
pip install mcp labkey

# Register MCP server with Claude Code (replace <repo-root> with your repository path)
claude mcp add labkey -- python <repo-root>/ai/mcp/LabKeyMcp/server.py
```

### Credential Configuration

Create a netrc file in your home directory:

- **Unix/macOS**: `~/.netrc`
- **Windows**: `~/.netrc` or `~/_netrc`

```
machine skyline.ms
login yourname+claude@domain.com
password <password>
```

> **Note**: Use your personal `+claude` account. Ask a team lead to add your account to the "Agents" group on skyline.ms.

### Verify Setup

```bash
# Replace <repo-root> with your repository path
python <repo-root>/ai/mcp/LabKeyMcp/test_connection.py
```

## Usage

After setup, Claude Code can query exceptions directly:

**Query recent exceptions:**
> "Show me exceptions from the last 7 days"

**Get specific exception:**
> "Get details for exception #73598"

**Explore data:**
> "What schemas are available on skyline.ms?"

## Daily Triage Workflow

### Quick Start

Use the `/pw-exceptions` command or call directly:

```
save_exceptions_report(report_date="2025-12-24")
```

This saves a full report to `ai/.tmp/exceptions-report-YYYYMMDD.md` with:
- Summary table of all exceptions for the day
- Full stack traces for each exception
- User comments and contact info

### Triage Steps

1. **Generate daily report**: `save_exceptions_report(report_date="YYYY-MM-DD")`
2. **Review summary table**: Identify exception types and frequencies
3. **Filter noise**: Ignore old versions, repeat reports from same installation
4. **Identify patterns**: Group by exception type and location
5. **Prioritize**: Focus on current version, multiple users affected
6. **Investigate**: Cross-reference stack traces with codebase
7. **Propose fixes**: For clear-cut issues, generate fix candidates

## Future Enhancements

- Exception grouping by stack trace signature
- Version filtering (current release vs older)
- User impact analysis (unique installations affected)
- Code correlation (map stack traces to current codebase)
- Fix proposal generation

## Related Documentation

- [MCP Development Guide](mcp-development-guide.md) - Patterns for extending MCP capabilities
- [Nightly Test Analysis](nightly-test-analysis.md) - Test results data access
- [Wiki and Support Board System](wiki-support-system.md) - Documentation and support access
- [LabKey MCP Server README](../mcp/LabKeyMcp/README.md) - Setup instructions
- [Query Documentation](../mcp/LabKeyMcp/queries/README.md) - Server-side query reference
- [MCP Python SDK](https://github.com/modelcontextprotocol/python-sdk)
- [LabKey Python API](https://github.com/LabKey/labkey-api-python)
