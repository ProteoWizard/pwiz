"""Common utilities for LabKey MCP server.

This module contains:
- Constants for default server configuration
- Shared helper functions (credentials, HTTP requests)
- Limited discovery (list_queries only - for proposing schema documentation)
"""

import json
import logging
import base64
import netrc
import urllib.error
import urllib.request
from pathlib import Path
from urllib.parse import quote, urlencode

import labkey
from labkey.query import ServerContext

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

# Issues schema
ISSUES_SCHEMA = "issues"
DEFAULT_ISSUES_CONTAINER = "/home/issues"


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
# Limited Discovery Tools
# =============================================================================

def register_tools(mcp):
    """Register limited discovery tools.

    Only list_queries is exposed - enough to see what tables exist,
    but not enough to poke around with raw queries. When Claude needs
    data from a table, it should propose schema documentation work
    rather than trying to query directly.
    """

    @mcp.tool()
    async def list_queries(
        schema_name: str,
        server: str = DEFAULT_SERVER,
        container_path: str = DEFAULT_CONTAINER,
    ) -> str:
        """**DISCOVERY**: See available tables/queries. Use to propose schema documentation, not raw queries.

        Args:
            schema_name: Schema name (e.g., 'testresults', 'issues', 'wiki')
            container_path: Container path (e.g., '/home/development/Nightly x64')
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
                lines = [
                    f"Queries/tables in '{schema_name}' at {container_path}:",
                    "",
                ]
                for q in sorted(queries, key=lambda x: x.get("name", "")):
                    name = q.get("name", "unknown")
                    title = q.get("title", "")
                    if title and title != name:
                        lines.append(f"  - {name} ({title})")
                    else:
                        lines.append(f"  - {name}")

                lines.extend([
                    "",
                    "To access a table, propose schema documentation:",
                    "  1. Create stub: LabKeyMcp/queries/{schema}/{table}-schema.md",
                    "  2. Human populates from LabKey Schema Browser",
                    "  3. Design server-side query as .sql file",
                    "  4. Add high-level MCP tool",
                ])
                return "\n".join(lines)
            return f"No queries found in schema '{schema_name}'."

        except Exception as e:
            logger.error(f"Error listing queries: {e}", exc_info=True)
            return f"Error listing queries: {e}"

    @mcp.tool()
    async def fetch_labkey_page(
        view_name: str,
        server: str = DEFAULT_SERVER,
        container_path: str = DEFAULT_TEST_CONTAINER,
        params: dict = None,
    ) -> str:
        """**EXPLORATION**: Fetch an authenticated LabKey page (HTML).

        Use this to explore LabKey pages that developers view in browsers.
        Returns raw HTML content which can be parsed for data.

        Args:
            view_name: Page to fetch (e.g., 'project-begin.view', 'testresults-showRun.view')
            server: LabKey server hostname (default: skyline.ms)
            container_path: Container path (e.g., '/home/development/Nightly x64')
            params: Optional query parameters as dict (e.g., {'runId': 12345})

        Examples:
            fetch_labkey_page('project-begin.view', container_path='/home/development/Nightly x64')
            fetch_labkey_page('testresults-showRun.view', params={'runId': 79500})
        """
        try:
            # Build URL with proper encoding for paths with spaces
            encoded_path = quote(container_path, safe="/")
            url = f"https://{server}{encoded_path}/{view_name}"
            if params:
                url = f"{url}?{urlencode(params)}"

            logger.info(f"Fetching page: {url}")
            response_bytes = make_authenticated_request(server, url, timeout=60)
            html_content = response_bytes.decode('utf-8', errors='replace')

            # Save to file to avoid overwhelming context
            # Generate filename from view_name, params, and date
            from datetime import datetime
            import re
            date_stamp = datetime.now().strftime("%Y%m%d")
            safe_view = view_name.replace('.view', '').replace('-', '_')
            folder = container_path.split('/')[-1].replace(' ', '_')
            if params:
                # Sanitize param values to be filesystem-safe
                param_str = '_'.join(f"{k}{re.sub(r'[^a-zA-Z0-9]', '', str(v))}" for k, v in params.items())
                filename = f"page-{safe_view}-{param_str}-{date_stamp}.html"
            else:
                filename = f"page-{safe_view}-{folder}-{date_stamp}.html"

            filepath = get_tmp_dir() / filename
            filepath.write_text(html_content, encoding='utf-8')

            # Return summary and filepath
            lines = [
                f"Saved {len(html_content):,} chars to: {filepath}",
                f"URL: {url}",
                "",
                "Use Read tool to examine the file content.",
            ]
            return "\n".join(lines)

        except urllib.error.HTTPError as e:
            return f"HTTP Error {e.code}: {e.reason}"
        except Exception as e:
            logger.error(f"Error fetching page: {e}", exc_info=True)
            return f"Error fetching page: {e}"
