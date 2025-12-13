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
from typing import Optional

import labkey
from labkey.query import QueryFilter
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
    return labkey.utils.create_server_context(
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

    with urllib.request.urlopen(request) as response:
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
    """
    try:
        server_context = get_server_context(server, container_path)

        # Build filter if provided
        filter_array = None
        if filter_column and filter_value:
            filter_array = [QueryFilter(filter_column, filter_value, "eq")]

        result = labkey.query.select_rows(
            server_context=server_context,
            schema_name=schema_name,
            query_name=query_name,
            max_rows=max_rows,
            filter_array=filter_array,
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


def main():
    """Run the MCP server."""
    logger.info("Starting LabKey MCP server")
    mcp.run(transport="stdio")


if __name__ == "__main__":
    main()
