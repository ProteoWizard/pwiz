# LabKey MCP Server

MCP (Model Context Protocol) server for querying LabKey/Panorama servers, specifically designed for Skyline exception triage and nightly test analysis at skyline.ms.

## Setup

### 1. Install Dependencies

```bash
pip install mcp labkey
```

### 2. Configure Authentication

Create a netrc file with your skyline.ms credentials in the standard location:

- **Unix/macOS**: `~/.netrc`
- **Windows**: `~/.netrc` or `~/_netrc`

File contents:
```
machine skyline.ms
login your-email@example.com
password your-password
```

> **Security note:** The netrc file contains credentials in plain text. Ensure appropriate file permissions and never commit it to version control.

### 3. Register with Claude Code

```bash
claude mcp add labkey -- python C:/proj/pwiz/pwiz_tools/Skyline/Executables/DevTools/LabKeyMcp/server.py
```

Or add to your Claude Code settings (`~/.claude/settings.json`):

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

### 4. Verify Setup

```bash
python C:/proj/pwiz/pwiz_tools/Skyline/Executables/DevTools/LabKeyMcp/test_connection.py
```

## Available Tools

### Discovery Tools

| Tool | Description |
|------|-------------|
| `list_containers` | List child folders in a container |
| `list_schemas` | List available schemas in a container |
| `list_queries` | List queries/tables in a schema |
| `query_table` | Query data from any LabKey table |

### Exception Triage Tools

| Tool | Description |
|------|-------------|
| `query_exceptions(days, max_rows)` | Query recent exceptions, sorted by date |
| `get_exception_details(exception_id)` | Get full stack trace and details for an exception |

### Nightly Test Tools

| Tool | Description |
|------|-------------|
| `query_test_runs(days, max_rows)` | Query recent test runs with pass/fail/leak counts |
| `get_run_failures(run_id)` | Get failed tests and stack traces for a run |
| `get_run_leaks(run_id)` | Get memory and handle leaks for a run |

## Usage Examples

Once registered, Claude Code can use these tools:

```
"List the schemas available on skyline.ms"
"Query the last 7 days of exceptions"
"Get details for exception ID 12345"
"Show test runs from the last 14 days"
"What tests failed in run #79466?"
"Show memory leaks for run #79450"
```

## Data Locations on skyline.ms

| Data Type | Container Path |
|-----------|----------------|
| Exceptions | `/home/issues/exceptions` |
| Nightly Tests (x64) | `/home/development/Nightly x64` |

## Discovery Workflow

To explore what data is available:

1. Use `list_containers` to browse the folder structure
2. Use `list_schemas` to see available schemas in a container
3. Use `list_queries` to find tables within a schema
4. Use `query_table` to explore the data

## Development

```bash
# Run server directly for testing
python server.py

# The server waits for JSON-RPC messages on stdin
# Press Ctrl+C to exit
```

## Related Documentation

- [Exception Triage System](../../../ai/docs/exception-triage-system.md) - Full workflow documentation
- [Developer Setup Guide](../../../ai/docs/developer-setup-guide.md) - Environment configuration
