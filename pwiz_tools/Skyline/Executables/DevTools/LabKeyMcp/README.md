# LabKey MCP Server

MCP (Model Context Protocol) server for querying LabKey/Panorama servers, specifically designed for Skyline exception triage at skyline.ms.

## Setup

### 1. Install Dependencies

```bash
cd pwiz_tools/Skyline/Executables/DevTools/LabKeyMcp
pip install -e .
# Or install directly:
pip install mcp labkey
```

### 2. Configure Authentication

Create a `_netrc` file in your home directory (Windows) or `.netrc` (Unix):

```
machine skyline.ms
login your-email@example.com
password your-password
```

**Windows path**: `C:\Users\YourName\_netrc`
**Unix path**: `~/.netrc`

### 3. Register with Claude Code

```bash
claude mcp add labkey -- python C:/proj/pwiz/pwiz_tools/Skyline/Executables/DevTools/LabKeyMcp/server.py
```

Or add to `.mcp.json` in your project:

```json
{
  "mcpServers": {
    "labkey": {
      "command": "python",
      "args": ["C:/proj/pwiz/pwiz_tools/Skyline/Executables/DevTools/LabKeyMcp/server.py"]
    }
  }
}
```

## Available Tools

| Tool | Description |
|------|-------------|
| `list_schemas` | List available schemas in a container |
| `list_queries` | List queries/tables in a schema |
| `list_containers` | List child folders in a container |
| `query_table` | Query data from any LabKey table |
| `query_exceptions` | Convenience wrapper for exception queries |
| `get_exception_details` | Get full details for a specific exception |

## Usage Examples

Once registered, Claude Code can use these tools:

```
"List the schemas available on skyline.ms"
"Query the last 7 days of exceptions"
"Get details for exception ID 12345"
```

## Discovery Workflow

Since the exact schema structure on skyline.ms needs to be discovered:

1. Use `list_containers` to find where exception data is stored
2. Use `list_schemas` to see available schemas in that container
3. Use `list_queries` to find exception-related tables
4. Use `query_table` to explore the data

## Development

```bash
# Run server directly for testing
python server.py

# The server waits for JSON-RPC messages on stdin
# Press Ctrl+C to exit
```
