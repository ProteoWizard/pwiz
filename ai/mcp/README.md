# MCP Servers for AI-Assisted Development

This directory contains Model Context Protocol (MCP) servers that enable Claude Code to interact with external resources important to the ProteoWizard/Skyline project.

## Available Servers

| Server | Description |
|--------|-------------|
| [LabKeyMcp](LabKeyMcp/) | Access to skyline.ms LabKey server (exceptions, nightly tests, wiki, support board) |

## Purpose

MCP servers provide structured access to project resources that would otherwise require manual navigation or complex API calls. They enable Claude Code to:

- Query nightly test results and analyze failures
- Review exception reports from Skyline users
- Read and update wiki documentation
- Monitor support board activity
- Access file attachments

## Adding New Servers

When creating new MCP servers for this project:

1. Create a subdirectory with the server name (e.g., `GitHubMcp/`)
2. Include a README.md explaining the server's purpose and tools
3. Document any server-side queries in a `queries/` subdirectory
4. Follow the patterns established in LabKeyMcp

## Configuration

MCP servers are configured in the user's Claude Code settings. See [LabKeyMcp/README.md](LabKeyMcp/README.md) for configuration details.

## Related Documentation

- [ai-context-branch-strategy.md](../docs/ai-context-branch-strategy.md) - How AI infrastructure is managed
- [development-guide.md](../docs/mcp/development-guide.md) - Patterns for MCP development
