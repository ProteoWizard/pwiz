"""Common utilities and discovery tools for LabKey MCP server.

This module contains:
- Constants for default server configuration
- Shared helper functions (credentials, HTTP requests)
- Discovery tools (list_schemas, list_queries, list_containers, query_table)
"""

import json
import logging
import base64
import netrc
import urllib.request
from pathlib import Path
from typing import Optional, Union
from urllib.parse import quote, urlencode

import labkey
from labkey.query import QueryFilter, ServerContext

logger = logging.getLogger("labkey_mcp")

# =============================================================================
# Default Server Configuration
# =============================================================================

DEFAULT_SERVER = "skyline.ms"
DEFAULT_CONTAINER = "/home/issues/exceptions"

# Exception data schema (discovered from skyline.ms)
EXCEPTION_SCHEMA = "announcement"
EXCEPTION_QUERY = "Announcement"

# Testresults schema
TESTRESULTS_SCHEMA = "testresults"
DEFAULT_TEST_CONTAINER = "/home/development/Nightly x64"

# Wiki schema
WIKI_SCHEMA = "wiki"
DEFAULT_WIKI_CONTAINER = "/home/software/Skyline"

# Support board schema
ANNOUNCEMENT_SCHEMA_SUPPORT = "announcement"
DEFAULT_SUPPORT_CONTAINER = "/home/support"


# =============================================================================
# Shared Helper Functions
# =============================================================================

def get_server_context(server: str, container_path: str) -> ServerContext:
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


def get_netrc_credentials(server: str) -> tuple[str, str]:
    """Get credentials from netrc file.

    Args:
        server: The server hostname to get credentials for

    Returns:
        Tuple of (login, password)

    Raises:
        Exception: If no credentials found for server
    """
    home = Path.home()
    netrc_paths = [home / ".netrc", home / "_netrc"]

    for netrc_path in netrc_paths:
        if netrc_path.exists():
            try:
                nrc = netrc.netrc(str(netrc_path))
                auth = nrc.authenticators(server)
                if auth:
                    login, _, password = auth
                    return login, password
            except Exception:
                continue

    raise Exception(f"No credentials found for {server} in netrc")


def make_authenticated_request(
    server: str,
    url: str,
    method: str = "GET",
    data: bytes = None,
    headers: dict = None,
    timeout: int = 30
) -> bytes:
    """Make an authenticated HTTP request to a LabKey server.

    Args:
        server: Server hostname for credential lookup
        url: Full URL to request
        method: HTTP method (GET, POST, etc.)
        data: Optional request body bytes
        headers: Optional additional headers
        timeout: Request timeout in seconds

    Returns:
        Response body as bytes
    """
    login, password = get_netrc_credentials(server)

    request = urllib.request.Request(url, data=data, method=method)
    credentials = base64.b64encode(f"{login}:{password}".encode()).decode()
    request.add_header("Authorization", f"Basic {credentials}")

    if headers:
        for key, value in headers.items():
            request.add_header(key, value)

    with urllib.request.urlopen(request, timeout=timeout) as response:
        return response.read()


def discovery_request(server: str, container_path: str, api_action: str, params: dict = None) -> dict:
    """Make a discovery API request using credentials from netrc.

    The labkey SDK doesn't expose getSchemas/getQueries/getContainers directly,
    so we use direct HTTP requests with authentication.

    Args:
        server: LabKey server hostname
        container_path: Container/folder path
        api_action: API action (e.g., 'query-getSchemas.api')
        params: Optional query parameters

    Returns:
        Parsed JSON response as dict
    """
    # Build URL with proper encoding for paths with spaces
    encoded_path = quote(container_path, safe="/")
    url = f"https://{server}{encoded_path}/{api_action}"
    if params:
        url = f"{url}?{urlencode(params)}"

    response_bytes = make_authenticated_request(server, url)
    return json.loads(response_bytes.decode())


def get_tmp_dir() -> Path:
    """Get the ai/.tmp directory for saving files.

    Creates the directory if it doesn't exist.

    Returns:
        Path to ai/.tmp directory
    """
    # Navigate from tools/ -> LabKeyMcp/ -> mcp/ -> ai/ -> .tmp/
    tmp_dir = Path(__file__).parent.parent.parent.parent / ".tmp"
    tmp_dir.mkdir(exist_ok=True)
    return tmp_dir


# =============================================================================
# Discovery Tools
# =============================================================================

def register_tools(mcp):
    """Register discovery and general query tools."""

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
            result = discovery_request(server, container_path, "query-getSchemas.api")

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
            result = discovery_request(
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
            data = discovery_request(
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
