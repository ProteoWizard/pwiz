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
DEFAULT_CONTAINER = "/home"  # Adjust based on actual skyline.ms structure


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
                {"fieldKey": filter_column, "op": "eq", "value": filter_value}
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
    server: str = DEFAULT_SERVER,
    container_path: str = DEFAULT_CONTAINER,
    max_rows: int = 50,
) -> str:
    """Query recent exceptions from Skyline exception tracking.

    This is a convenience wrapper for querying exception data.
    Note: You may need to adjust schema_name and query_name based on
    the actual skyline.ms configuration.

    Args:
        days: Number of days back to query (default: 7)
        server: LabKey server hostname (default: skyline.ms)
        container_path: Container path where exceptions are stored
        max_rows: Maximum rows to return (default: 50)
    """
    try:
        api = get_api(server, container_path)

        # Calculate date filter
        since_date = (datetime.now() - timedelta(days=days)).strftime("%Y-%m-%d")

        # Try common exception table names
        # These may need adjustment based on actual skyline.ms schema
        possible_schemas = ["lists", "exp", "core"]
        possible_queries = ["Exceptions", "ExceptionReport", "exceptions"]

        for schema in possible_schemas:
            for query in possible_queries:
                try:
                    result = api.query.select_rows(
                        schema_name=schema,
                        query_name=query,
                        max_rows=max_rows,
                    )
                    if result and result.get("rows"):
                        rows = result["rows"]
                        lines = [
                            f"Found {len(rows)} exceptions in {schema}.{query}:",
                            "",
                        ]
                        for i, row in enumerate(rows[:max_rows], 1):
                            lines.append(f"--- Exception {i} ---")
                            for key, value in row.items():
                                if not key.startswith("_"):
                                    if isinstance(value, str) and len(value) > 300:
                                        value = value[:300] + "..."
                                    lines.append(f"  {key}: {value}")
                            lines.append("")
                        return "\n".join(lines)
                except Exception:
                    continue

        return (
            f"Could not find exception data. "
            f"Use list_schemas and list_queries to discover the correct location."
        )

    except Exception as e:
        logger.error(f"Error querying exceptions: {e}", exc_info=True)
        return f"Error querying exceptions: {e}"


@mcp.tool()
async def get_exception_details(
    exception_id: str,
    server: str = DEFAULT_SERVER,
    container_path: str = DEFAULT_CONTAINER,
    schema_name: str = "lists",
    query_name: str = "Exceptions",
    id_column: str = "RowId",
) -> str:
    """Get full details for a specific exception by ID.

    Args:
        exception_id: The exception ID to look up
        server: LabKey server hostname (default: skyline.ms)
        container_path: Container path where exceptions are stored
        schema_name: Schema containing exception data (default: lists)
        query_name: Query/table name for exceptions (default: Exceptions)
        id_column: Column name for exception ID (default: RowId)
    """
    try:
        api = get_api(server, container_path)

        result = api.query.select_rows(
            schema_name=schema_name,
            query_name=query_name,
            filter_array=[{"fieldKey": id_column, "op": "eq", "value": exception_id}],
            max_rows=1,
        )

        if result and result.get("rows"):
            row = result["rows"][0]
            lines = [f"Exception {exception_id} details:", ""]
            for key, value in row.items():
                if not key.startswith("_"):
                    lines.append(f"{key}:")
                    lines.append(f"  {value}")
                    lines.append("")
            return "\n".join(lines)
        return f"No exception found with {id_column}={exception_id}"

    except Exception as e:
        logger.error(f"Error getting exception details: {e}", exc_info=True)
        return f"Error getting exception details: {e}"


def main():
    """Run the MCP server."""
    logger.info("Starting LabKey MCP server")
    mcp.run(transport="stdio")


if __name__ == "__main__":
    main()
