# MCP Server Documentation

This folder contains documentation for MCP (Model Context Protocol) servers used by Claude Code.

## MCP Servers

| Server | Purpose | Documentation |
|--------|---------|---------------|
| LabKey | skyline.ms data access (wiki, support, exceptions, tests) | See sections below |
| Gmail | Email sending for automated reports | [gmail.md](gmail.md) |

---

# LabKey MCP Server

Access skyline.ms data via the LabKey MCP server.

## Data Sources

| Document | Container | Description |
|----------|-----------|-------------|
| [announcements.md](announcements.md) | (multiple) | General-purpose table: release notes, support, exceptions |
| [wiki.md](wiki.md) | `/home/software/Skyline` | Wiki pages, tutorials, documentation |
| [support.md](support.md) | `/home/support` | Support board threads and user questions |
| [exceptions.md](exceptions.md) | `/home/issues/exceptions` | User-reported crash reports |
| [nightly-tests.md](nightly-tests.md) | `/home/development/Nightly x64` | Automated test results |
| [issues-strategy.md](issues-strategy.md) | `/home/issues` | Issue tracking analysis |

## Architecture

```
Claude Code
    │
    └── MCP Protocol (stdio)
            │
            └── LabKeyMcp Server (Python)
                    │
                    ├── labkey Python SDK (queries)
                    │       │
                    │       └── skyline.ms LabKey Server
                    │               ├── /home/software/Skyline (wiki)
                    │               ├── /home/support (announcements)
                    │               ├── /home/issues/exceptions
                    │               └── /home/development/Nightly x64
                    │
                    └── HTTP requests (updates, attachments)
```

## Authentication

Each developer uses a personal `+claude` account:
- **Team members**: `yourname+claude@proteinms.net`
- **Interns/others**: `yourname+claude@gmail.com`
- **Group**: "Agents" on skyline.ms

> **Important**: The `+claude` suffix only works with Gmail-backed email providers (@proteinms.net, @gmail.com). It will **not** work with @uw.edu or similar providers.

Credentials are stored in `~/.netrc`:
```
machine skyline.ms
login yourname+claude@domain.com
password <password>
```

## Tool Selection

**Important**: Start with PRIMARY tools - see [tool-hierarchy.md](tool-hierarchy.md) for:
- Which tools to use first (PRIMARY vs DRILL-DOWN vs DISCOVERY)
- Usage patterns and anti-patterns
- When to propose new tools

## Development

- [development-guide.md](development-guide.md) - Patterns for extending MCP capabilities
- [Server source code](../../mcp/LabKeyMcp/) - Python implementation
- [Query documentation](../../mcp/LabKeyMcp/queries/README.md) - Server-side queries

## Setup

See [Developer Setup Guide](../developer-setup-guide.md) for installation instructions.

## MCP Server Registration

### Configuration File Location

MCP servers are registered in **`~/.claude.json`** (your home directory). This single file stores all Claude Code configuration, including per-project MCP server definitions.

The structure for MCP servers:
```json
{
  "projects": {
    "C:/proj/pwiz-ai": {
      "mcpServers": {
        "labkey": {
          "type": "stdio",
          "command": "python",
          "args": ["./ai/mcp/LabKeyMcp/server.py"],
          "env": {}
        },
        "gmail": {
          "type": "stdio",
          "command": "npx",
          "args": ["@gongrzhe/server-gmail-autoauth-mcp"],
          "env": {}
        }
      }
    }
  }
}
```

To register a server interactively: `claude mcp add <name>`
To list registered servers: `claude mcp list`

### Context Impact of MCP Servers

**Important**: Each registered MCP server consumes context tokens for tool definitions.

| Component | Approximate Tokens |
|-----------|-------------------|
| LabKey MCP (25 tools) | ~18k tokens |
| Gmail MCP (19 tools) | ~13k tokens |
| **Total MCP overhead** | **~31k tokens (15% of 200k context)** |

This overhead is incurred at the start of every session where MCP servers are enabled.

### Recommended: Separate Directories for Different Workflows

To maximize coding context, consider maintaining **two separate checkouts**:

| Directory | Branch | MCP Servers | Purpose |
|-----------|--------|-------------|---------|
| `C:\proj\pwiz-ai` | `ai-context` | LabKey + Gmail | Daily reports, documentation, scheduled tasks |
| `C:\proj\pwiz` | `master` or feature | None | Active coding with maximum context |

**Why this helps**:
- Coding sessions get full 200k context for code, tests, and exploration
- Daily report sessions have the MCP tools they need
- No context wasted on unused tools

**Setup**:
1. Keep existing `pwiz-ai` checkout with MCP servers for ai-context work
2. Use a separate checkout (e.g., `pwiz`, `pwiz-feature`) without MCP registration for coding
3. The MCP servers are project-specific in `~/.claude.json`, so different directories can have different configurations

## Command-Line Automation

**Important**: MCP tools require explicit permission to work in non-interactive mode (`claude -p`).

**Wildcards do NOT work** - each tool must be listed by name in `.claude/settings.local.json`.

To configure permissions for a command-line operation:
1. Start an interactive Claude Code session
2. Describe the automation you need
3. Ask Claude to write the appropriate `permissions.allow` list
4. Review and remove any destructive tools you don't want auto-approved

See [Scheduled Tasks Guide](../scheduled-tasks-guide.md#critical-mcp-permissions-for-command-line-automation) for the complete example.
