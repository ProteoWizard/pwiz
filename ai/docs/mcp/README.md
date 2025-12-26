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

## Development

- [development-guide.md](development-guide.md) - Patterns for extending MCP capabilities
- [Server source code](../../mcp/LabKeyMcp/) - Python implementation
- [Query documentation](../../mcp/LabKeyMcp/queries/README.md) - Server-side queries

## Setup

See [Developer Setup Guide](../developer-setup-guide.md) for installation instructions.
