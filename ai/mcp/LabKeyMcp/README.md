# LabKey MCP Server

MCP (Model Context Protocol) server for querying LabKey/Panorama servers, specifically designed for Skyline exception triage and nightly test analysis at skyline.ms.

## Setup

### 1. Install Dependencies

```bash
pip install mcp labkey
```

### 2. Configure Authentication

Create a personal `+claude` account for MCP access:
- **Team members**: `yourname+claude@proteinms.net`
- **Interns/others**: `yourname+claude@gmail.com`
- Ask a team lead to add your account to the "Agents" group on skyline.ms

> **Important**: The `+claude` suffix only works with Gmail-backed email providers (@proteinms.net, @gmail.com). It will **not** work with @uw.edu or similar providers.

Create a netrc file with your credentials in the standard location:

- **Unix/macOS**: `~/.netrc`
- **Windows**: `~/.netrc` or `~/_netrc`

File contents:
```
machine skyline.ms
login yourname+claude@domain.com
password your-password
```

> **Why +claude accounts?** Individual accounts provide attribution for any edits made via Claude, while the Agents group restricts permissions to least-privilege access.

### 3. Register with Claude Code

```bash
# Replace <repo-root> with your actual repository path
claude mcp add labkey -- python <repo-root>/ai/mcp/LabKeyMcp/server.py
```

Or add to your Claude Code settings (`~/.claude/settings.json`):

```json
{
  "mcpServers": {
    "labkey": {
      "command": "python",
      "args": ["<repo-root>/ai/mcp/LabKeyMcp/server.py"]
    }
  }
}
```

### 4. Verify Setup

```bash
# Replace <repo-root> with your actual repository path
python <repo-root>/ai/mcp/LabKeyMcp/test_connection.py
```

## Available Tools

### Discovery Tools

| Tool | Description |
|------|-------------|
| `list_containers` | List child folders in a container |
| `list_schemas` | List available schemas in a container |
| `list_queries` | List queries/tables in a schema |
| `query_table` | Query data from any LabKey table or custom query |

The `query_table` tool supports a `parameters` argument (JSON object or string) for server-side parameterized queries:

```
query_table(
    schema_name="testresults",
    query_name="testruns_detail",
    container_path="/home/development/Nightly x64",
    parameters={"StartDate": "2025-12-13", "EndDate": "2025-12-14"}
)
```

### Exception Triage Tools

| Tool | Description |
|------|-------------|
| `save_exceptions_report(report_date)` | Generate daily report, save to `ai/.tmp/exceptions-report-YYYYMMDD.md` |
| `query_exceptions(days, max_rows)` | Query recent exceptions, returns summary |
| `get_exception_details(exception_id)` | Get full stack trace and details for an exception |

### Nightly Test Tools

| Tool | Description |
|------|-------------|
| `get_daily_test_summary(report_date)` | Query all 6 folders, save report to ai/.tmp/ |
| `save_test_failure_history(test_name, start_date, container_path)` | Collect stack traces, detect patterns |
| `query_test_runs(days, max_rows)` | Query recent test runs with pass/fail/leak counts |
| `get_run_failures(run_id)` | Get failed tests and stack traces for a run |
| `get_run_leaks(run_id)` | Get memory and handle leaks for a run |
| `save_run_log(run_id)` | Save full test run log to ai/.tmp/ for grep/search |

The `get_daily_test_summary(report_date)` tool is the primary entry point for daily test review. It queries all 6 test folders, saves a full markdown report to `ai/.tmp/nightly-report-YYYYMMDD.md`, and returns a brief summary with action items.

For **historical analysis** (How long has this been failing? When did it last pass?), use `query_table` with `testruns_detail` and a date range:
```
query_table(schema_name="testresults", query_name="testruns_detail",
            container_path="/home/development/Nightly x64",
            parameters={"StartDate": "2025-12-01", "EndDate": "2025-12-15"})
```

The `save_run_log(run_id)` tool saves the complete 9-12 hour test run output to `ai/.tmp/testrun-log-{run_id}.txt` for deep investigation.

The `save_test_failure_history(test_name, start_date, container_path)` tool collects all stack traces for a specific test, groups them by pattern, and saves to `ai/.tmp/test-failures-{testname}.md`. This helps determine if multiple failures share the same root cause.

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
| Nightly x64 | `/home/development/Nightly x64` |
| Performance Tests | `/home/development/Performance Tests` |
| Release Branch | `/home/development/Release Branch` |
| Release Branch Performance Tests | `/home/development/Release Branch Performance Tests` |
| Integration | `/home/development/Integration` |
| Integration with Perf Tests | `/home/development/Integration with Perf Tests` |

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

- [Nightly Tests](../../docs/mcp/nightly-tests.md) - Test analysis workflow and queries
- [Exceptions](../../docs/mcp/exceptions.md) - Exception workflow documentation
- [Developer Setup Guide](../../docs/developer-setup-guide.md) - Environment configuration
