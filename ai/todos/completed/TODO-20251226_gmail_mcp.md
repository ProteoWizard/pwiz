# Gmail MCP Integration

- **Created**: 2025-12-26
- **GitHub Issue**: https://github.com/ProteoWizard/pwiz/issues/3733
- **Branch**: ai-context

## Objective

Set up Gmail MCP server for `claude.c.skyline@gmail.com` to enable Claude Code to send emails automatically.

## Tasks

- [x] Research Gmail MCP server options
- [x] Configure authentication (OAuth)
- [x] Register Gmail MCP server with Claude Code
- [x] Test sending a simple email
- [x] Document setup in ai/docs/mcp/gmail.md

## Progress Log

### 2025-12-26 - Session Start

Starting research on Gmail MCP server options...

**Research completed:**
- Evaluated multiple Gmail MCP servers
- Checked GitHub activity/stars for project health
- Selected GongRzhe/Gmail-MCP-Server (874 stars, actively maintained)
- Created documentation: ai/docs/mcp/gmail.md

**Human steps completed:**
- Google Cloud Console setup (project, Gmail API, OAuth consent, credentials)
- Browser authentication for claude.c.skyline@gmail.com
- MCP server registration with Claude Code

**Testing completed:**
- Sent test email successfully (ID: 19b5bdb4d89bd57c)
- Gmail MCP server shows connected in `claude mcp list`

**Documentation updated:**
- Added Quick Start section for new developers
- Separated one-time GCP setup from per-developer setup
- Fixed Windows-specific commands (PowerShell instead of bash)

**Workflow gap identified:**
- We did not push TODO to ai-context on start (should signal ownership)
- We did not comment on GitHub Issue #3733 with branch/TODO location
- Updated workflow-issues-guide.md to require both signals when starting work
- Future sprints should: (1) push TODO to ai-context immediately, (2) comment on issue
