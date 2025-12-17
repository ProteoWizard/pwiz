"""MCP Server for LabKey/Panorama exception triage.

This server exposes tools for querying Skyline exception logs from skyline.ms
and other LabKey servers. Authentication is handled via netrc file.

Setup:
1. Create _netrc file (Windows) or .netrc (Unix) in your home directory:
   machine skyline.ms
   login your-email@example.com
   password your-password

2. Install dependencies:
   pip install mcp labkey

3. Register with Claude Code:
   claude mcp add labkey -- python /path/to/server.py
"""

import logging
from datetime import datetime, timedelta
from typing import Any, Optional, Union

import labkey
from labkey.query import QueryFilter, ServerContext
from mcp.server.fastmcp import FastMCP

# Configure logging to stderr (required for STDIO transport)
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
)
logger = logging.getLogger("labkey_mcp")

# Initialize FastMCP server
mcp = FastMCP("labkey")

# Default server configuration
DEFAULT_SERVER = "skyline.ms"
DEFAULT_CONTAINER = "/home/issues/exceptions"

# Exception data schema (discovered from skyline.ms)
EXCEPTION_SCHEMA = "announcement"
EXCEPTION_QUERY = "Announcement"

# Testresults schema
TESTRESULTS_SCHEMA = "testresults"
DEFAULT_TEST_CONTAINER = "/home/development/Nightly x64"


def get_server_context(server: str, container_path: str):
    """Create a LabKey server context for API calls.

    Authentication is handled automatically via netrc file in standard locations:
    - ~/.netrc (Unix/Windows)
    - ~/_netrc (Windows)
    """
    return ServerContext(
        server,
        container_path,
        use_ssl=True,
    )


# =============================================================================
# Discovery Tools - Use direct HTTP since SDK doesn't expose these APIs
# =============================================================================

def _discovery_request(server: str, container_path: str, api_action: str, params: dict = None) -> dict:
    """Make a discovery API request using the SDK's server context for auth.

    The labkey SDK doesn't expose getSchemas/getQueries/getContainers directly,
    so we use its internal request mechanism.
    """
    import json
    import urllib.request
    import netrc
    import base64
    from pathlib import Path
    from urllib.parse import quote, urlencode

    # Get credentials from netrc (same locations the SDK uses)
    home = Path.home()
    netrc_paths = [home / ".netrc", home / "_netrc"]

    login, password = None, None
    for netrc_path in netrc_paths:
        if netrc_path.exists():
            try:
                nrc = netrc.netrc(str(netrc_path))
                auth = nrc.authenticators(server)
                if auth:
                    login, _, password = auth
                    break
            except Exception:
                continue

    if not login:
        raise Exception(f"No credentials found for {server} in netrc")

    # Build URL with proper encoding for paths with spaces
    encoded_path = quote(container_path, safe="/")
    url = f"https://{server}{encoded_path}/{api_action}"
    if params:
        url = f"{url}?{urlencode(params)}"

    # Make authenticated request
    request = urllib.request.Request(url)
    credentials = base64.b64encode(f"{login}:{password}".encode()).decode()
    request.add_header("Authorization", f"Basic {credentials}")

    with urllib.request.urlopen(request, timeout=30) as response:
        return json.loads(response.read().decode())


@mcp.tool()
async def list_schemas(
    server: str = DEFAULT_SERVER,
    container_path: str = DEFAULT_CONTAINER,
) -> str:
    """List available schemas in a LabKey container.

    Use this to discover what data is available before querying.

    Args:
        server: LabKey server hostname (default: skyline.ms)
        container_path: Container/folder path (default: /home)
    """
    try:
        result = _discovery_request(server, container_path, "query-getSchemas.api")

        if result and "schemas" in result:
            schemas = []
            for s in result["schemas"]:
                if isinstance(s, dict):
                    schemas.append(s.get("schemaName", str(s)))
                else:
                    schemas.append(str(s))
            return f"Available schemas in {container_path}:\n" + "\n".join(
                f"  - {s}" for s in sorted(schemas)
            )
        return f"No schemas found. Raw result: {result}"

    except Exception as e:
        logger.error(f"Error listing schemas: {e}", exc_info=True)
        return f"Error listing schemas: {e}"


@mcp.tool()
async def list_queries(
    schema_name: str,
    server: str = DEFAULT_SERVER,
    container_path: str = DEFAULT_CONTAINER,
) -> str:
    """List available queries/tables in a LabKey schema.

    Args:
        schema_name: Schema name (e.g., 'lists', 'core', 'exp')
        server: LabKey server hostname (default: skyline.ms)
        container_path: Container/folder path (default: /home)
    """
    try:
        result = _discovery_request(
            server,
            container_path,
            "query-getQueries.api",
            {"schemaName": schema_name}
        )

        if result and "queries" in result:
            queries = result["queries"]
            lines = [f"Queries in schema '{schema_name}':"]
            for q in sorted(queries, key=lambda x: x.get("name", "")):
                name = q.get("name", "unknown")
                title = q.get("title", "")
                if title and title != name:
                    lines.append(f"  - {name} ({title})")
                else:
                    lines.append(f"  - {name}")
            return "\n".join(lines)
        return f"No queries found in schema '{schema_name}'. Raw result: {result}"

    except Exception as e:
        logger.error(f"Error listing queries: {e}", exc_info=True)
        return f"Error listing queries: {e}"


@mcp.tool()
async def list_containers(
    server: str = DEFAULT_SERVER,
    parent_path: str = "/",
) -> str:
    """List child containers (folders) in a LabKey server.

    Use this to explore the folder structure and find exception data.

    Args:
        server: LabKey server hostname (default: skyline.ms)
        parent_path: Parent container path to list children of (default: /)
    """
    try:
        data = _discovery_request(
            server,
            parent_path,
            "project-getContainers.api",
            {"includeSubfolders": "false"}
        )

        lines = [f"Containers under '{parent_path}':"]

        if isinstance(data, dict):
            children = data.get("children", [])
            if children:
                for child in children:
                    name = child.get("name", "unknown")
                    path = child.get("path", "")
                    lines.append(f"  - {name} ({path})")
            elif "name" in data:
                lines.append(f"  Container: {data.get('name')} ({data.get('path', parent_path)})")
                lines.append("  (No child containers)")
        elif isinstance(data, list):
            for container in data:
                name = container.get("name", "unknown")
                path = container.get("path", "")
                lines.append(f"  - {name} ({path})")

        return "\n".join(lines) if len(lines) > 1 else "No child containers found."

    except Exception as e:
        logger.error(f"Error listing containers: {e}", exc_info=True)
        return f"Error listing containers: {e}"


# =============================================================================
# Data Query Tools - Use official LabKey Python SDK
# =============================================================================

@mcp.tool()
async def query_table(
    schema_name: str,
    query_name: str,
    server: str = DEFAULT_SERVER,
    container_path: str = DEFAULT_CONTAINER,
    max_rows: int = 100,
    filter_column: Optional[str] = None,
    filter_value: Optional[str] = None,
    parameters: Union[str, dict, None] = None,
) -> str:
    """Query data from a LabKey table.

    Args:
        schema_name: Schema name (e.g., 'lists', 'core')
        query_name: Query/table name
        server: LabKey server hostname (default: skyline.ms)
        container_path: Container/folder path (default: /home)
        max_rows: Maximum rows to return (default: 100)
        filter_column: Optional column name to filter on
        filter_value: Optional value to filter for (requires filter_column)
        parameters: Optional JSON string of query parameters (e.g., '{"StartDate": "2025-12-07", "EndDate": "2025-12-14"}')
    """
    import json

    try:
        server_context = get_server_context(server, container_path)

        # Build filter if provided
        filter_array = None
        if filter_column and filter_value:
            filter_array = [QueryFilter(filter_column, filter_value, "eq")]

        # Parse parameters - accept either dict or JSON string
        params_dict = None
        if parameters:
            if isinstance(parameters, dict):
                params_dict = parameters
            else:
                params_dict = json.loads(parameters)

        result = labkey.query.select_rows(
            server_context=server_context,
            schema_name=schema_name,
            query_name=query_name,
            max_rows=max_rows,
            filter_array=filter_array,
            parameters=params_dict,
        )

        if result and "rows" in result:
            rows = result["rows"]
            row_count = result.get("rowCount", len(rows))

            if not rows:
                return "No rows found."

            lines = [f"Found {row_count} rows (showing up to {max_rows}):"]
            lines.append("")

            if rows:
                columns = list(rows[0].keys())
                columns = [c for c in columns if not c.startswith("_")]

                for i, row in enumerate(rows[:max_rows], 1):
                    lines.append(f"--- Row {i} ---")
                    for col in columns:
                        value = row.get(col, "")
                        if isinstance(value, str) and len(value) > 200:
                            value = value[:200] + "..."
                        lines.append(f"  {col}: {value}")
                    lines.append("")

            return "\n".join(lines)
        return "No results returned."

    except Exception as e:
        logger.error(f"Error querying table: {e}", exc_info=True)
        return f"Error querying table: {e}"


@mcp.tool()
async def query_exceptions(
    days: int = 7,
    max_rows: int = 50,
    server: str = DEFAULT_SERVER,
    container_path: str = DEFAULT_CONTAINER,
) -> str:
    """Query recent exceptions from Skyline exception tracking on skyline.ms.

    Returns exception reports submitted by Skyline users, including:
    - Exception type and title
    - User comments and email
    - Stack trace (in FormattedBody)
    - Created/Modified dates

    Args:
        days: Number of days back to query (default: 7)
        max_rows: Maximum rows to return (default: 50)
        server: LabKey server hostname (default: skyline.ms)
        container_path: Container path (default: /home/issues/exceptions)
    """
    try:
        server_context = get_server_context(server, container_path)

        # Calculate date filter
        since_date = (datetime.now() - timedelta(days=days)).strftime("%Y-%m-%d")
        filter_array = [QueryFilter("Created", since_date, "dategte")]

        result = labkey.query.select_rows(
            server_context=server_context,
            schema_name=EXCEPTION_SCHEMA,
            query_name=EXCEPTION_QUERY,
            max_rows=max_rows,
            sort="-Created",
            filter_array=filter_array,
        )

        if result and result.get("rows"):
            rows = result["rows"]
            total = result.get("rowCount", len(rows))
            lines = [
                f"Found {total} exceptions in the last {days} days (showing {len(rows)}):",
                "",
            ]
            for i, row in enumerate(rows, 1):
                title = row.get("Title", "Unknown")
                created = row.get("Created", "Unknown")
                row_id = row.get("RowId", "?")
                status = row.get("Status") or "Unassigned"
                body = row.get("FormattedBody", "")

                body_preview = body[:200] + "..." if len(body) > 200 else body

                lines.append(f"--- Exception #{row_id} ---")
                lines.append(f"  Title: {title}")
                lines.append(f"  Created: {created}")
                lines.append(f"  Status: {status}")
                lines.append(f"  Preview: {body_preview}")
                lines.append("")

            return "\n".join(lines)
        else:
            return f"No exceptions found in the last {days} days."

    except Exception as e:
        logger.error(f"Error querying exceptions: {e}", exc_info=True)
        return f"Error querying exceptions: {e}"


@mcp.tool()
async def get_exception_details(
    exception_id: int,
    server: str = DEFAULT_SERVER,
    container_path: str = DEFAULT_CONTAINER,
) -> str:
    """Get full details for a specific exception by RowId.

    Returns complete exception information including:
    - Full stack trace
    - User email and comments
    - Skyline version
    - Installation ID

    Args:
        exception_id: The RowId of the exception to look up
        server: LabKey server hostname (default: skyline.ms)
        container_path: Container path (default: /home/issues/exceptions)
    """
    try:
        server_context = get_server_context(server, container_path)
        filter_array = [QueryFilter("RowId", str(exception_id), "eq")]

        result = labkey.query.select_rows(
            server_context=server_context,
            schema_name=EXCEPTION_SCHEMA,
            query_name=EXCEPTION_QUERY,
            max_rows=1,
            filter_array=filter_array,
        )

        if result and result.get("rows"):
            row = result["rows"][0]
            lines = [f"Exception #{exception_id} Full Details:", ""]

            lines.append(f"Title: {row.get('Title', 'Unknown')}")
            lines.append(f"Created: {row.get('Created', 'Unknown')}")
            lines.append(f"Modified: {row.get('Modified', 'Unknown')}")
            lines.append(f"Status: {row.get('Status') or 'Unassigned'}")
            lines.append(f"Assigned To: {row.get('AssignedTo') or 'Nobody'}")
            lines.append("")

            lines.append("=== Full Report ===")
            lines.append(row.get("FormattedBody", "No body content"))
            lines.append("")

            return "\n".join(lines)
        return f"No exception found with RowId={exception_id}"

    except Exception as e:
        logger.error(f"Error getting exception details: {e}", exc_info=True)
        return f"Error getting exception details: {e}"


# =============================================================================
# Testresults Tools - For nightly test run analysis
# =============================================================================

@mcp.tool()
async def query_test_runs(
    days: int = 7,
    max_rows: int = 20,
    server: str = DEFAULT_SERVER,
    container_path: str = DEFAULT_TEST_CONTAINER,
) -> str:
    """Query recent test runs from Skyline nightly testing.

    Returns test run summaries including:
    - Run date and duration
    - Passed/failed/leaked test counts
    - Average memory usage
    - Git revision

    Args:
        days: Number of days back to query (default: 7)
        max_rows: Maximum rows to return (default: 20)
        server: LabKey server hostname (default: skyline.ms)
        container_path: Container path (default: /home/development/Nightly x64)
    """
    try:
        server_context = get_server_context(server, container_path)

        since_date = (datetime.now() - timedelta(days=days)).strftime("%Y-%m-%d")
        filter_array = [QueryFilter("posttime", since_date, "dategte")]

        result = labkey.query.select_rows(
            server_context=server_context,
            schema_name=TESTRESULTS_SCHEMA,
            query_name="testruns",
            max_rows=max_rows,
            sort="-posttime",
            filter_array=filter_array,
        )

        if result and result.get("rows"):
            rows = result["rows"]
            total = result.get("rowCount", len(rows))
            lines = [
                f"Found {total} test runs in the last {days} days (showing {len(rows)}):",
                "",
            ]
            for row in rows:
                run_id = row.get("id", "?")
                posttime = row.get("posttime", "Unknown")
                passed = row.get("passedtests", 0)
                failed = row.get("failedtests", 0)
                leaked = row.get("leakedtests", 0)
                duration = row.get("duration", 0)
                avg_mem = row.get("averagemem", 0)
                revision = row.get("revision", "?")
                flagged = row.get("flagged", False)

                status = "FLAGGED " if flagged else ""
                lines.append(f"--- Run #{run_id} ({posttime}) {status}---")
                lines.append(f"  Tests: {passed} passed, {failed} failed, {leaked} leaked")
                lines.append(f"  Duration: {duration} min, Avg Memory: {avg_mem} MB")
                lines.append(f"  Revision: {revision}")
                lines.append("")

            return "\n".join(lines)
        return f"No test runs found in the last {days} days."

    except Exception as e:
        logger.error(f"Error querying test runs: {e}", exc_info=True)
        return f"Error querying test runs: {e}"


@mcp.tool()
async def get_run_failures(
    run_id: int,
    server: str = DEFAULT_SERVER,
    container_path: str = DEFAULT_TEST_CONTAINER,
) -> str:
    """Get failed tests for a specific test run.

    Returns details about each failed test including:
    - Test name
    - Stack trace
    - Pass number (which attempt failed)

    Args:
        run_id: The test run ID to look up
        server: LabKey server hostname (default: skyline.ms)
        container_path: Container path (default: /home/development/Nightly x64)
    """
    try:
        server_context = get_server_context(server, container_path)
        filter_array = [QueryFilter("testrunid", str(run_id), "eq")]

        result = labkey.query.select_rows(
            server_context=server_context,
            schema_name=TESTRESULTS_SCHEMA,
            query_name="testfails",
            max_rows=100,
            filter_array=filter_array,
        )

        if result and result.get("rows"):
            rows = result["rows"]
            lines = [f"Failed tests for run #{run_id} ({len(rows)} failures):", ""]

            for row in rows:
                testname = row.get("testname", "Unknown")
                stacktrace = row.get("stacktrace", "")
                pass_num = row.get("pass", "?")

                lines.append(f"=== {testname} (pass {pass_num}) ===")
                if len(stacktrace) > 1000:
                    stacktrace = stacktrace[:1000] + "\n... (truncated)"
                lines.append(stacktrace)
                lines.append("")

            return "\n".join(lines)
        return f"No failures found for run #{run_id}"

    except Exception as e:
        logger.error(f"Error getting run failures: {e}", exc_info=True)
        return f"Error getting run failures: {e}"


@mcp.tool()
async def save_run_log(
    run_id: int,
    server: str = DEFAULT_SERVER,
    container_path: str = DEFAULT_TEST_CONTAINER,
) -> str:
    """Fetch test run log and save to ai/.tmp/testrun-log-{run_id}.txt for grep/search.

    The log contains the full 9-12 hour test run output including git checkout,
    build output, and all test execution output. This is useful for deep investigation
    when stack traces from testfails aren't sufficient.

    Returns metadata only (not the log content) to avoid context bloat:
    - file_path: where the log was saved
    - size_bytes: file size
    - line_count: number of lines

    Args:
        run_id: The test run ID to fetch the log for
        server: LabKey server hostname (default: skyline.ms)
        container_path: Container path (default: /home/development/Nightly x64)
    """
    from pathlib import Path

    try:
        server_context = get_server_context(server, container_path)
        filter_array = [QueryFilter("id", str(run_id), "eq")]

        result = labkey.query.select_rows(
            server_context=server_context,
            schema_name=TESTRESULTS_SCHEMA,
            query_name="testruns",
            max_rows=1,
            filter_array=filter_array,
            columns=["id", "log"],
        )

        if not result or not result.get("rows"):
            return f"No test run found with id={run_id}"

        row = result["rows"][0]
        log_content = row.get("log", "")

        if not log_content:
            return f"Test run #{run_id} has no log content"

        # Determine output path relative to this script
        # server.py is in: pwiz_tools/Skyline/Executables/DevTools/LabKeyMcp/
        # ai/.tmp/ is in: ai/.tmp/
        script_dir = Path(__file__).resolve().parent
        repo_root = script_dir.parent.parent.parent.parent.parent
        output_dir = repo_root / "ai" / ".tmp"
        output_dir.mkdir(parents=True, exist_ok=True)

        output_file = output_dir / f"testrun-log-{run_id}.txt"
        output_file.write_text(log_content, encoding="utf-8")

        # Calculate metadata
        size_bytes = output_file.stat().st_size
        line_count = log_content.count("\n") + 1

        return (
            f"Log saved successfully:\n"
            f"  file_path: {output_file}\n"
            f"  size_bytes: {size_bytes:,}\n"
            f"  line_count: {line_count:,}\n"
            f"\nUse Grep or Read tools to search within this file."
        )

    except Exception as e:
        logger.error(f"Error saving run log: {e}", exc_info=True)
        return f"Error saving run log: {e}"


@mcp.tool()
async def get_run_leaks(
    run_id: int,
    server: str = DEFAULT_SERVER,
    container_path: str = DEFAULT_TEST_CONTAINER,
) -> str:
    """Get memory and handle leaks for a specific test run.

    Returns details about each leaked test including:
    - Test name
    - Bytes leaked (for memory leaks)
    - Leak type

    Args:
        run_id: The test run ID to look up
        server: LabKey server hostname (default: skyline.ms)
        container_path: Container path (default: /home/development/Nightly x64)
    """
    try:
        server_context = get_server_context(server, container_path)
        filter_array = [QueryFilter("testrunid", str(run_id), "eq")]

        # Query both memory leaks and handle leaks
        mem_result = labkey.query.select_rows(
            server_context=server_context,
            schema_name=TESTRESULTS_SCHEMA,
            query_name="memoryleaks",
            max_rows=100,
            filter_array=filter_array,
        )

        handle_result = labkey.query.select_rows(
            server_context=server_context,
            schema_name=TESTRESULTS_SCHEMA,
            query_name="handleleaks",
            max_rows=100,
            filter_array=filter_array,
        )

        lines = [f"Leaks for run #{run_id}:", ""]

        mem_rows = mem_result.get("rows", []) if mem_result else []
        handle_rows = handle_result.get("rows", []) if handle_result else []

        if mem_rows:
            lines.append(f"=== Memory Leaks ({len(mem_rows)}) ===")
            for row in mem_rows:
                testname = row.get("testname", "Unknown")
                bytes_leaked = row.get("bytes", 0)
                lines.append(f"  {testname}: {bytes_leaked:,} bytes")
            lines.append("")

        if handle_rows:
            lines.append(f"=== Handle Leaks ({len(handle_rows)}) ===")
            for row in handle_rows:
                testname = row.get("testname", "Unknown")
                handles = row.get("handles", row.get("count", "?"))
                lines.append(f"  {testname}: {handles} handles")
            lines.append("")

        if not mem_rows and not handle_rows:
            return f"No leaks found for run #{run_id}"

        return "\n".join(lines)

    except Exception as e:
        logger.error(f"Error getting run leaks: {e}", exc_info=True)
        return f"Error getting run leaks: {e}"


@mcp.tool()
async def get_daily_test_summary(
    report_date: str,
    server: str = DEFAULT_SERVER,
) -> str:
    """Query all 6 nightly test folders and save a consolidated report for one day.

    This is the primary tool for daily test review. It queries all folders
    in one call and saves a full report to ai/.tmp/nightly-report-YYYYMMDD.md.

    Args:
        report_date: Date in YYYY-MM-DD format (e.g., "2025-12-15")
        server: LabKey server hostname (default: skyline.ms)

    Returns:
        Brief summary with file path. Full details are in the saved file.
    """
    from pathlib import Path

    # Query for runs on this date (use same date for start and end)
    start_date = report_date
    end_date = report_date
    # All 6 test folders with their expected durations
    folders = [
        ("/home/development/Nightly x64", 540),
        ("/home/development/Release Branch", 540),
        ("/home/development/Performance Tests", 720),
        ("/home/development/Release Branch Performance Tests", 720),
        ("/home/development/Integration", 540),
        ("/home/development/Integration with Perf Tests", 720),
    ]

    all_results = []
    summary_lines = [
        f"# Daily Test Summary: {report_date}",
        "",
        "## Summary by Folder",
        "",
        "| Folder | Runs | Pass | Fail | Leak | Anomaly |",
        "|--------|------|------|------|------|---------|",
    ]

    total_tests = 0
    runs_with_failures = []
    runs_with_leaks = []
    runs_with_hangs = []

    for container_path, expected_duration in folders:
        folder_name = container_path.split("/")[-1]
        try:
            server_context = get_server_context(server, container_path)

            # Query expected computers with their trained values
            expected_result = labkey.query.select_rows(
                server_context=server_context,
                schema_name=TESTRESULTS_SCHEMA,
                query_name="expected_computers",
                max_rows=100,
            )
            expected_computers = {}
            if expected_result and expected_result.get("rows"):
                for ec in expected_result["rows"]:
                    comp_name = ec.get("computer", "")
                    if comp_name:
                        expected_computers[comp_name] = {
                            "meantestsrun": ec.get("meantestsrun", 0),
                            "stddevtestsrun": ec.get("stddevtestsrun", 1),
                            "meanmemory": ec.get("meanmemory", 0),
                            "stddevmemory": ec.get("stddevmemory", 1),
                        }

            result = labkey.query.select_rows(
                server_context=server_context,
                schema_name=TESTRESULTS_SCHEMA,
                query_name="testruns_detail",
                max_rows=50,
                parameters={"StartDate": start_date, "EndDate": end_date},
            )

            rows = result.get("rows", []) if result else []

            if not rows:
                summary_lines.append(f"| {folder_name} | 0 | - | - | - | - |")
                continue

            # Analyze runs
            pass_count = 0
            fail_count = 0
            leak_count = 0
            anomaly_count = 0
            computers_seen = set()

            folder_data = {
                "folder": folder_name,
                "container_path": container_path,
                "runs": [],
                "missing_computers": [],
            }

            for row in rows:
                run_id = row.get("run_id", "?")
                computer = row.get("computer", "?")
                duration = row.get("duration", 0)
                passed = row.get("passedtests", 0)
                failed = row.get("failedtests", 0)
                leaked = row.get("leakedtests", 0)
                githash = row.get("githash", "?")
                posttime = row.get("posttime", "?")
                averagemem = row.get("averagemem", 0)
                hung_test = row.get("hung_test")
                hung_language = row.get("hung_language")

                computers_seen.add(computer)
                total_tests += passed

                # Classify run using trained stddev if available
                is_anomaly = False
                anomaly_reason = ""

                if computer in expected_computers:
                    ec = expected_computers[computer]
                    mean_tests = ec["meantestsrun"]
                    stddev_tests = ec["stddevtestsrun"]
                    if stddev_tests > 0:
                        z_score = abs(passed - mean_tests) / stddev_tests
                        if z_score >= 4:
                            is_anomaly = True
                            anomaly_reason = f"4σ ({passed} vs {mean_tests:.0f})"
                        elif z_score >= 3:
                            is_anomaly = True
                            anomaly_reason = f"3σ ({passed} vs {mean_tests:.0f})"

                # Fallback: Short duration = crash
                if not is_anomaly and duration < expected_duration * 0.9:
                    is_anomaly = True
                    anomaly_reason = f"short ({duration} min)"

                if failed > 0:
                    fail_count += 1
                    runs_with_failures.append({
                        "run_id": run_id,
                        "folder": folder_name,
                        "container_path": container_path,
                        "computer": computer,
                        "failed": failed
                    })
                elif leaked > 0:
                    leak_count += 1
                    runs_with_leaks.append({
                        "run_id": run_id,
                        "folder": folder_name,
                        "container_path": container_path,
                        "computer": computer,
                        "leaked": leaked
                    })
                elif is_anomaly:
                    anomaly_count += 1
                else:
                    pass_count += 1

                # Track hangs separately (a run can have both failures and hangs)
                if hung_test:
                    runs_with_hangs.append({
                        "run_id": run_id,
                        "folder": folder_name,
                        "container_path": container_path,
                        "computer": computer,
                        "hung_test": hung_test,
                        "hung_language": hung_language,
                    })

                folder_data["runs"].append({
                    "run_id": run_id,
                    "computer": computer,
                    "posttime": str(posttime)[:16] if posttime else "?",
                    "duration": duration,
                    "passed": passed,
                    "failed": failed,
                    "leaked": leaked,
                    "memory": averagemem,
                    "githash": str(githash)[:9] if githash else "?",
                    "anomaly": anomaly_reason,
                    "hung_test": hung_test,
                    "hung_language": hung_language,
                })

            # Find missing computers
            missing = set(expected_computers.keys()) - computers_seen
            folder_data["missing_computers"] = sorted(missing)

            all_results.append(folder_data)

            # Include missing count in anomaly column
            missing_count = len(missing)
            anomaly_display = anomaly_count
            if missing_count > 0:
                anomaly_display = f"{anomaly_count}+{missing_count}m"

            summary_lines.append(
                f"| {folder_name} | {len(rows)} | {pass_count} | {fail_count} | {leak_count} | {anomaly_display} |"
            )

        except Exception as e:
            logger.error(f"Error querying {folder_name}: {e}")
            summary_lines.append(f"| {folder_name} | ERROR | - | - | - | - |")

    summary_lines.extend([
        "",
        f"**Total tests run**: {total_tests:,}",
        "",
    ])

    # Add details for each folder with data
    for folder_data in all_results:
        if not folder_data["runs"]:
            continue

        summary_lines.extend([
            f"## {folder_data['folder']}",
            "",
            "| Computer | Memory | Tests | PostTime | Duration | Fail | Leak | Hung | Git Hash | Anomaly |",
            "|----------|--------|-------|----------|----------|------|------|------|----------|---------|",
        ])

        for run in folder_data["runs"]:
            hung_display = run.get('hung_test', '') or ''
            if hung_display and run.get('hung_language'):
                hung_display = f"{hung_display} ({run['hung_language']})"
            summary_lines.append(
                f"| {run['computer']} | {run['memory']} | {run['passed']} | {run['posttime']} | "
                f"{run['duration']} | {run['failed']} | {run['leaked']} | {hung_display} | {run['githash']} | {run['anomaly']} |"
            )

        # Add missing computers if any
        if folder_data.get("missing_computers"):
            summary_lines.append("")
            summary_lines.append(f"**Missing**: {', '.join(folder_data['missing_computers'])}")

        summary_lines.append("")

    # Query failures and leaks by test name for each folder with issues
    all_failures = []  # (testname, computer, folder)
    all_leaks = []  # (testname, computer, folder, leak_type)

    for folder_data in all_results:
        if not folder_data["runs"]:
            continue

        container_path = folder_data["container_path"]
        folder_name = folder_data["folder"]

        # Check if this folder had any failures or leaks
        has_failures = any(r["failed"] > 0 for r in folder_data["runs"])
        has_leaks = any(r["leaked"] > 0 for r in folder_data["runs"])

        try:
            server_context = get_server_context(server, container_path)

            if has_failures:
                fail_result = labkey.query.select_rows(
                    server_context=server_context,
                    schema_name=TESTRESULTS_SCHEMA,
                    query_name="failures_by_date",
                    max_rows=100,
                    parameters={"StartDate": start_date, "EndDate": end_date},
                )
                if fail_result and fail_result.get("rows"):
                    for row in fail_result["rows"]:
                        all_failures.append({
                            "testname": row.get("testname", "?"),
                            "computer": row.get("computer", "?"),
                            "folder": folder_name,
                        })

            if has_leaks:
                leak_result = labkey.query.select_rows(
                    server_context=server_context,
                    schema_name=TESTRESULTS_SCHEMA,
                    query_name="leaks_by_date",
                    max_rows=100,
                    parameters={"StartDate": start_date, "EndDate": end_date},
                )
                if leak_result and leak_result.get("rows"):
                    for row in leak_result["rows"]:
                        all_leaks.append({
                            "testname": row.get("testname", "?"),
                            "computer": row.get("computer", "?"),
                            "folder": folder_name,
                            "leak_type": row.get("leak_type", "?"),
                        })
        except Exception as e:
            logger.error(f"Error querying failures/leaks for {folder_name}: {e}")

    # Group failures by test name
    if all_failures:
        summary_lines.extend([
            "## Failures by Test",
            "",
            "| Test | Computers | Folder |",
            "|------|-----------|--------|",
        ])
        # Group by (testname, folder)
        from collections import defaultdict
        failure_groups = defaultdict(list)
        for f in all_failures:
            key = (f["testname"], f["folder"])
            failure_groups[key].append(f["computer"])

        for (testname, folder), computers in sorted(failure_groups.items()):
            computers_str = ", ".join(sorted(set(computers)))
            summary_lines.append(f"| {testname} | {computers_str} | {folder} |")
        summary_lines.append("")

    # Group leaks by test name
    if all_leaks:
        summary_lines.extend([
            "## Leaks by Test",
            "",
            "| Test | Type | Computers | Folder |",
            "|------|------|-----------|--------|",
        ])
        # Group by (testname, folder, leak_type)
        from collections import defaultdict
        leak_groups = defaultdict(list)
        for l in all_leaks:
            key = (l["testname"], l["folder"], l["leak_type"])
            leak_groups[key].append(l["computer"])

        for (testname, folder, leak_type), computers in sorted(leak_groups.items()):
            computers_str = ", ".join(sorted(set(computers)))
            summary_lines.append(f"| {testname} | {leak_type} | {computers_str} | {folder} |")
        summary_lines.append("")

    # Group hangs by test name
    if runs_with_hangs:
        summary_lines.extend([
            "## Hangs by Test",
            "",
            "| Test | Language | Computers | Folder |",
            "|------|----------|-----------|--------|",
        ])
        # Group by (testname, folder, language)
        from collections import defaultdict
        hang_groups = defaultdict(list)
        for h in runs_with_hangs:
            key = (h["hung_test"], h["folder"], h.get("hung_language", "?"))
            hang_groups[key].append(h["computer"])

        for (testname, folder, language), computers in sorted(hang_groups.items()):
            computers_str = ", ".join(sorted(set(computers)))
            summary_lines.append(f"| {testname} | {language} | {computers_str} | {folder} |")
        summary_lines.append("")

    # Write full report to file
    report_content = "\n".join(summary_lines)

    # Determine output path
    script_dir = Path(__file__).resolve().parent
    repo_root = script_dir.parent.parent.parent.parent.parent
    output_dir = repo_root / "ai" / ".tmp"
    output_dir.mkdir(parents=True, exist_ok=True)

    # Use report_date for filename
    date_str = report_date.replace("-", "")
    output_file = output_dir / f"nightly-report-{date_str}.md"
    output_file.write_text(report_content, encoding="utf-8")

    # Build brief summary for return
    total_runs = sum(len(fd["runs"]) for fd in all_results)
    total_failures = len(runs_with_failures)
    total_leaks = len(runs_with_leaks)
    total_hangs = len(runs_with_hangs)
    total_missing = sum(len(fd.get("missing_computers", [])) for fd in all_results)

    brief = [
        f"Daily test report saved to: {output_file}",
        "",
        f"Summary: {total_runs} runs across {len([f for f in all_results if f['runs']])} folders",
        f"  - Total tests: {total_tests:,}",
        f"  - Runs with failures: {total_failures}",
        f"  - Runs with leaks: {total_leaks}",
        f"  - Runs with hangs: {total_hangs}",
        f"  - Missing computers: {total_missing}",
        "",
    ]

    if runs_with_failures:
        brief.append("Failures to investigate:")
        for r in runs_with_failures:
            brief.append(f"  - get_run_failures({r['run_id']})  # {r['folder']}/{r['computer']}")

    if runs_with_leaks:
        brief.append("Leaks to investigate:")
        for r in runs_with_leaks:
            brief.append(f"  - get_run_leaks({r['run_id']})  # {r['folder']}/{r['computer']}")

    if runs_with_hangs:
        brief.append("Hangs to investigate:")
        for h in runs_with_hangs:
            brief.append(f"  - {h['hung_test']} ({h.get('hung_language', '?')})  # {h['folder']}/{h['computer']}")

    brief.append("")
    brief.append(f"See {output_file} for full details.")

    return "\n".join(brief)


@mcp.tool()
async def save_test_failure_history(
    test_name: str,
    start_date: str,
    end_date: Optional[str] = None,
    server: str = DEFAULT_SERVER,
    container_path: str = DEFAULT_TEST_CONTAINER,
) -> str:
    """Save failure history for a specific test to a file for analysis.

    Collects stack traces and saves to ai/.tmp/test-failures-{testname}.md.

    This enables:
    - Comparing stack traces across multiple failures
    - Pattern recognition to determine if failures share the same root cause
    - Historical analysis to see when a failure started

    Args:
        test_name: The test name to search for (e.g., "TestPanoramaDownloadFile")
        start_date: Start date in YYYY-MM-DD format
        end_date: End date in YYYY-MM-DD format (default: same as start_date)
        server: LabKey server hostname (default: skyline.ms)
        container_path: Container path (default: /home/development/Nightly x64)

    Returns:
        Summary with file path. Full stack traces are in the saved file.
    """
    from pathlib import Path
    from collections import defaultdict

    # Use same date for start and end if not specified (single day query)
    if not end_date:
        end_date = start_date
    folder_name = container_path.split("/")[-1]

    all_failures = []

    try:
        server_context = get_server_context(server, container_path)

        # Get failures for this test in the date range
        fail_result = labkey.query.select_rows(
            server_context=server_context,
            schema_name=TESTRESULTS_SCHEMA,
            query_name="failures_by_date",
            max_rows=500,
            parameters={"StartDate": start_date, "EndDate": end_date},
        )

        if not fail_result or not fail_result.get("rows"):
            return f"No failures found in '{folder_name}' for {start_date} to {end_date}."

        # Filter for our specific test and collect run IDs
        matching_runs = {}
        for row in fail_result["rows"]:
            if row.get("testname") == test_name:
                run_id = row.get("testrunid")
                computer = row.get("computer", "?")
                posttime = row.get("posttime", "?")
                if run_id:
                    matching_runs[run_id] = {
                        "computer": computer,
                        "posttime": str(posttime)[:16] if posttime else "?",
                    }

        if not matching_runs:
            return f"No failures found for test '{test_name}' in '{folder_name}' ({start_date} to {end_date})."

        # Get the full stack traces from testfails for each run
        for run_id, run_info in matching_runs.items():
            try:
                stack_result = labkey.query.select_rows(
                    server_context=server_context,
                    schema_name=TESTRESULTS_SCHEMA,
                    query_name="testfails",
                    max_rows=10,
                    filter_array=[
                        QueryFilter("testrunid", str(run_id), "eq"),
                        QueryFilter("testname", test_name, "eq"),
                    ],
                )

                if stack_result and stack_result.get("rows"):
                    for row in stack_result["rows"]:
                        all_failures.append({
                            "run_id": run_id,
                            "computer": run_info["computer"],
                            "posttime": run_info["posttime"],
                            "pass_num": row.get("pass", "?"),
                            "stacktrace": row.get("stacktrace", "No stack trace"),
                        })
            except Exception as e:
                logger.error(f"Error getting stack trace for run {run_id}: {e}")

    except Exception as e:
        logger.error(f"Error querying {folder_name}: {e}")
        return f"Error querying {folder_name}: {e}"

    if not all_failures:
        return f"No failures found for test '{test_name}' in '{folder_name}' ({start_date} to {end_date})."

    # Build the report
    lines = [
        f"# Failure History: {test_name}",
        f"",
        f"**Folder**: {folder_name}",
        f"**Date range**: {start_date} to {end_date}",
        f"**Total failures**: {len(all_failures)}",
        f"",
        "## Summary",
        "",
        "| Computer | Date | Pass |",
        "|----------|------|------|",
    ]

    for f in all_failures:
        lines.append(f"| {f['computer']} | {f['posttime']} | {f['pass_num']} |")

    lines.extend(["", "## Stack Traces", ""])

    # Group by unique stack traces to identify patterns
    trace_groups = defaultdict(list)
    for f in all_failures:
        # Normalize stack trace for grouping (first 500 chars as key)
        trace_key = f["stacktrace"][:500] if f["stacktrace"] else "empty"
        trace_groups[trace_key].append(f)

    lines.append(f"**Unique stack trace patterns**: {len(trace_groups)}")
    lines.append("")

    if len(trace_groups) == 1:
        lines.append("All failures have the **same stack trace pattern** - likely same root cause.")
        lines.append("")

    # Output each unique pattern
    pattern_num = 0
    for trace_key, failures in trace_groups.items():
        pattern_num += 1
        computers = sorted(set(f["computer"] for f in failures))

        lines.extend([
            f"### Pattern {pattern_num} ({len(failures)} occurrences)",
            f"",
            f"**Computers**: {', '.join(computers)}",
            f"",
            "```",
            failures[0]["stacktrace"],
            "```",
            "",
        ])

    # Write to file
    report_content = "\n".join(lines)

    script_dir = Path(__file__).resolve().parent
    repo_root = script_dir.parent.parent.parent.parent.parent
    output_dir = repo_root / "ai" / ".tmp"
    output_dir.mkdir(parents=True, exist_ok=True)

    # Sanitize test name for filename
    safe_name = test_name.replace("/", "_").replace("\\", "_").replace(" ", "_")
    output_file = output_dir / f"test-failures-{safe_name}.md"
    output_file.write_text(report_content, encoding="utf-8")

    # Build brief summary
    unique_patterns = len(trace_groups)
    pattern_msg = "same root cause" if unique_patterns == 1 else f"{unique_patterns} different patterns"

    return (
        f"Failure history saved to: {output_file}\n"
        f"\n"
        f"Summary for '{test_name}' in {folder_name} ({start_date} to {end_date}):\n"
        f"  - Total failures: {len(all_failures)}\n"
        f"  - Unique stack trace patterns: {unique_patterns} ({pattern_msg})\n"
        f"  - Computers affected: {', '.join(sorted(set(f['computer'] for f in all_failures)))}\n"
        f"\n"
        f"See {output_file} for full stack traces."
    )


def main():
    """Run the MCP server."""
    logger.info("Starting LabKey MCP server")
    mcp.run(transport="stdio")


if __name__ == "__main__":
    main()
