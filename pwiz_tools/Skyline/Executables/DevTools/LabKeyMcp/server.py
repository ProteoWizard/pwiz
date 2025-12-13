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


def get_api(server: str = DEFAULT_SERVER, container_path: str = DEFAULT_CONTAINER):
    """Get a LabKey API wrapper instance.

    Args:
        server: LabKey server hostname (without https://)
        container_path: Container/folder path on the server

    Returns:
        APIWrapper instance configured for the server
    """
    from labkey.api_wrapper import APIWrapper

    return APIWrapper(server, container_path, "")


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
        api = get_api(server, container_path)
        result = api.query.get_schemas()

        if result and "schemas" in result:
            schemas = [s["schemaName"] for s in result["schemas"]]
            return f"Available schemas in {container_path}:\n" + "\n".join(
                f"  - {s}" for s in sorted(schemas)
            )
        return "No schemas found."

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
        api = get_api(server, container_path)
        result = api.query.get_queries(schema_name)

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
        return f"No queries found in schema '{schema_name}'."

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
        api = get_api(server, parent_path)
        # Use the container API to list children
        result = api.container.get_containers(include_subfolders=True)

        if result:
            lines = [f"Containers under '{parent_path}':"]
            for container in result:
                name = container.get("name", "unknown")
                path = container.get("path", "")
                lines.append(f"  - {name} ({path})")
            return "\n".join(lines) if len(lines) > 1 else "No child containers found."
        return "No containers found."

    except Exception as e:
        logger.error(f"Error listing containers: {e}", exc_info=True)
        return f"Error listing containers: {e}"


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
        api = get_api(server, container_path)

        # Build filter if provided
        filter_array = None
        if filter_column and filter_value:
            filter_array = [
                QueryFilter(filter_column, filter_value, QueryFilter.Types.EQUAL)
            ]

        result = api.query.select_rows(
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

            # Format output
            lines = [f"Found {row_count} rows (showing up to {max_rows}):"]
            lines.append("")

            # Get column names from first row
            if rows:
                columns = list(rows[0].keys())
                # Filter out internal LabKey columns
                columns = [c for c in columns if not c.startswith("_")]

                for i, row in enumerate(rows[:max_rows], 1):
                    lines.append(f"--- Row {i} ---")
                    for col in columns:
                        value = row.get(col, "")
                        # Truncate long values
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
        api = get_api(server, container_path)

        # Calculate date filter
        since_date = (datetime.now() - timedelta(days=days)).strftime("%Y-%m-%d")
        date_filter = QueryFilter(
            "Created", since_date, QueryFilter.Types.DATE_GREATER_THAN_OR_EQUAL
        )

        # Query the announcement.Announcement table with date filter
        result = api.query.select_rows(
            schema_name=EXCEPTION_SCHEMA,
            query_name=EXCEPTION_QUERY,
            max_rows=max_rows,
            sort="-Created",  # Most recent first
            filter_array=[date_filter],
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

                # Extract first few lines of body for summary
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
        api = get_api(server, container_path)

        id_filter = QueryFilter("RowId", str(exception_id), QueryFilter.Types.EQUAL)
        result = api.query.select_rows(
            schema_name=EXCEPTION_SCHEMA,
            query_name=EXCEPTION_QUERY,
            filter_array=[id_filter],
            max_rows=1,
        )

        if result and result.get("rows"):
            row = result["rows"][0]
            lines = [f"Exception #{exception_id} Full Details:", ""]

            # Key fields first
            lines.append(f"Title: {row.get('Title', 'Unknown')}")
            lines.append(f"Created: {row.get('Created', 'Unknown')}")
            lines.append(f"Modified: {row.get('Modified', 'Unknown')}")
            lines.append(f"Status: {row.get('Status') or 'Unassigned'}")
            lines.append(f"Assigned To: {row.get('AssignedTo') or 'Nobody'}")
            lines.append("")

            # Full body with stack trace
            lines.append("=== Full Report ===")
            lines.append(row.get("FormattedBody", "No body content"))
            lines.append("")

            return "\n".join(lines)
        return f"No exception found with RowId={exception_id}"

    except Exception as e:
        logger.error(f"Error getting exception details: {e}", exc_info=True)
        return f"Error getting exception details: {e}"


def main():
    """Run the MCP server."""
    logger.info("Starting LabKey MCP server")
    mcp.run(transport="stdio")


if __name__ == "__main__":
    main()
