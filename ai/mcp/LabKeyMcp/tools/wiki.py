"""Wiki tools for LabKey MCP server.

This module contains tools for reading and updating wiki pages on skyline.ms,
including the LabKey tutorial documentation.
"""

import base64
import http.cookiejar
import json
import logging
import re
import urllib.error
import urllib.request
from typing import Optional
from urllib.parse import quote, unquote, urlencode

import labkey

from .common import (
    get_server_context,
    get_netrc_credentials,
    get_tmp_dir,
    DEFAULT_SERVER,
    DEFAULT_WIKI_CONTAINER,
    WIKI_SCHEMA,
)

logger = logging.getLogger("labkey_mcp")


class LabKeySession:
    """A simple session class for making authenticated LabKey requests with CSRF support.

    Following the pattern from ReportErrorDlg.cs:313-342.
    Uses urllib and http.cookiejar to maintain session cookies.
    """

    def __init__(self, server: str, login: str, password: str):
        self.server = server
        self.login = login
        self.password = password
        self.csrf_token = None

        # Set up cookie jar for session management
        self.cookie_jar = http.cookiejar.CookieJar()
        self.opener = urllib.request.build_opener(
            urllib.request.HTTPCookieProcessor(self.cookie_jar)
        )

        # Add basic auth header
        credentials = base64.b64encode(f"{login}:{password}".encode()).decode()
        self.auth_header = f"Basic {credentials}"

    def _establish_session(self):
        """GET to establish session and get CSRF token."""
        url = f"https://{self.server}/project/home/begin.view?"
        request = urllib.request.Request(url)
        request.add_header("Authorization", self.auth_header)

        with self.opener.open(request, timeout=30) as response:
            # Extract CSRF token from cookies
            for cookie in self.cookie_jar:
                if cookie.name == "X-LABKEY-CSRF":
                    self.csrf_token = cookie.value
                    break

        if not self.csrf_token:
            raise Exception("CSRF token not found in session cookies")

    def get(self, url: str, params: dict = None, headers: dict = None) -> dict:
        """Make a GET request with session cookies."""
        if params:
            url = f"{url}?{urlencode(params)}"

        request = urllib.request.Request(url)
        request.add_header("Authorization", self.auth_header)
        if self.csrf_token:
            request.add_header("X-LABKEY-CSRF", self.csrf_token)
        if headers:
            for key, value in headers.items():
                request.add_header(key, value)

        with self.opener.open(request, timeout=30) as response:
            return json.loads(response.read().decode())

    def post_json(self, url: str, data: dict, headers: dict = None) -> tuple:
        """Make a POST request with JSON body. Returns (status_code, response_data)."""
        request = urllib.request.Request(url, method="POST")
        request.add_header("Authorization", self.auth_header)
        request.add_header("Content-Type", "application/json")
        if self.csrf_token:
            request.add_header("X-LABKEY-CSRF", self.csrf_token)
        if headers:
            for key, value in headers.items():
                request.add_header(key, value)

        json_data = json.dumps(data).encode("utf-8")
        request.data = json_data

        try:
            with self.opener.open(request, timeout=30) as response:
                return response.status, json.loads(response.read().decode())
        except urllib.error.HTTPError as e:
            return e.code, {"error": e.read().decode()[:500]}


def _get_labkey_session(server: str) -> tuple:
    """Create an authenticated session with CSRF token for LabKey POST requests.

    Following the pattern from ReportErrorDlg.cs:313-342:
    1. GET to /project/home/begin.view? to establish session
    2. Extract X-LABKEY-CSRF cookie
    3. Return session with CSRF token for use in POST requests

    Args:
        server: LabKey server hostname

    Returns:
        Tuple of (LabKeySession, csrf_token)
    """
    login, password = get_netrc_credentials(server)

    # Create session and establish CSRF token
    session = LabKeySession(server, login, password)
    session._establish_session()

    return session, session.csrf_token


def _encode_waf_body(content: str) -> str:
    """Encode wiki body content using LabKey's WAF (Web Application Firewall) format.

    The format is: /*{{base64/x-www-form-urlencoded/wafText}}*/[base64-encoded-url-encoded-content]

    Args:
        content: The raw wiki page content (HTML or text)

    Returns:
        WAF-encoded string ready for the "body" field
    """
    # Step 1: URL-encode the content (encodes special chars like <, >, &, etc.)
    url_encoded = quote(content, safe="")

    # Step 2: Base64 encode the URL-encoded content
    base64_encoded = base64.b64encode(url_encoded.encode("utf-8")).decode("ascii")

    # Step 3: Prepend the WAF hint comment
    return f"/*{{{{base64/x-www-form-urlencoded/wafText}}}}*/{base64_encoded}"


def _decode_waf_body(waf_content: str) -> str:
    """Decode wiki body content from LabKey's WAF format.

    Args:
        waf_content: WAF-encoded string from the "body" field

    Returns:
        The decoded wiki page content
    """
    # Check for WAF prefix
    prefix = "/*{{base64/x-www-form-urlencoded/wafText}}*/"
    if waf_content.startswith(prefix):
        base64_content = waf_content[len(prefix):]
        url_encoded = base64.b64decode(base64_content).decode("utf-8")
        return unquote(url_encoded)

    # If not WAF encoded, return as-is
    return waf_content


def _get_wiki_page_metadata(
    page_name: str,
    server: str,
    container_path: str,
    session: LabKeySession = None,
) -> tuple:
    """Get wiki page metadata needed for updates (entityId, rowId, pageVersionId).

    Fetches page metadata from two sources:
    1. Database query (wiki_page_content) for title and rendererType - authoritative
    2. Wiki edit page HTML for entityId, rowId, pageVersionId - not in wiki schema

    Args:
        page_name: Wiki page name
        server: LabKey server hostname
        container_path: Container path
        session: Optional existing session to reuse (avoids CSRF token mismatch)

    Returns:
        Tuple of (metadata_dict, session) where metadata_dict has:
        entityId, rowId, pageVersionId, name, title, rendererType, parent
    """
    if session is None:
        session, _ = _get_labkey_session(server)

    # Step 1: Query database for authoritative title and rendererType
    # These should NOT come from HTML parsing - they're database fields
    server_context = get_server_context(server, container_path)
    db_result = labkey.query.select_rows(
        server_context=server_context,
        schema_name=WIKI_SCHEMA,
        query_name="wiki_page_content",
        max_rows=1,
        parameters={"PageName": page_name},
    )

    if not db_result or not db_result.get("rows"):
        raise Exception(f"Wiki page '{page_name}' not found in database")

    db_row = db_result["rows"][0]
    db_title = db_row.get("Title", page_name)
    db_renderer = db_row.get("RendererType", "HTML")
    logger.info(f"Database title: '{db_title}', renderer: '{db_renderer}'")

    # Step 2: Fetch the wiki edit page for entityId, rowId, pageVersionId
    # These aren't exposed in the wiki schema but are required for the save API
    encoded_path = quote(container_path, safe="/")
    encoded_name = quote(page_name, safe="")
    url = f"https://{server}{encoded_path}/wiki-edit.view?name={encoded_name}"

    request = urllib.request.Request(url)
    request.add_header("Authorization", session.auth_header)

    with session.opener.open(request, timeout=30) as response:
        html = response.read().decode("utf-8")

    # Debug: Save HTML to temp file for inspection
    tmp_dir = get_tmp_dir()
    debug_file = tmp_dir / f"wiki-edit-{page_name}.html"
    debug_file.write_text(html, encoding="utf-8")
    logger.info(f"Saved wiki edit page HTML to {debug_file}")

    # Step 3: Extract entityId, rowId, pageVersionId from HTML
    # These fields appear in the LABKEY._wiki.setProps({...}) JavaScript call
    # We search for them directly rather than trying to parse the full JS object,
    # since the object contains a large body field that complicates regex matching
    entity_match = re.search(r"entityId:\s*'([^']+)'", html)
    row_match = re.search(r'rowId:\s*(\d+)', html)
    version_match = re.search(r'pageVersionId:\s*(\d+)', html)
    parent_match = re.search(r'parent:\s*(\d+)', html)

    if entity_match and row_match and version_match:
        return ({
            "entityId": entity_match.group(1),
            "rowId": int(row_match.group(1)),
            "name": page_name,
            "title": db_title,  # From database, not HTML
            "parent": parent_match.group(1) if parent_match else "",
            "pageVersionId": int(version_match.group(1)),
            "rendererType": db_renderer,  # From database, not HTML
        }, session)

    raise Exception(f"Could not find wiki metadata in edit page for '{page_name}'")


def register_tools(mcp):
    """Register wiki tools."""

    @mcp.tool()
    async def list_wiki_pages(
        server: str = DEFAULT_SERVER,
        container_path: str = DEFAULT_WIKI_CONTAINER,
    ) -> str:
        """List all wiki pages in a container with metadata (no body content).

        Returns page names, titles, renderer types, and last modified dates.
        Use get_wiki_page() to retrieve full content for a specific page.

        Args:
            server: LabKey server hostname (default: skyline.ms)
            container_path: Container path (default: /home/software/Skyline)
        """
        try:
            server_context = get_server_context(server, container_path)

            result = labkey.query.select_rows(
                server_context=server_context,
                schema_name=WIKI_SCHEMA,
                query_name="wiki_page_list",
                max_rows=500,
                sort="Name",
            )

            if result and result.get("rows"):
                rows = result["rows"]
                lines = [
                    f"Found {len(rows)} wiki pages in {container_path}:",
                    "",
                    "| Name | Title | Renderer | Modified |",
                    "|------|-------|----------|----------|",
                ]

                for row in rows:
                    name = row.get("Name", "?")
                    title = row.get("Title", "")[:40]
                    renderer = row.get("RendererType", "?")
                    modified = str(row.get("Modified", "?"))[:10]
                    lines.append(f"| {name} | {title} | {renderer} | {modified} |")

                return "\n".join(lines)
            return f"No wiki pages found in {container_path}."

        except Exception as e:
            logger.error(f"Error listing wiki pages: {e}", exc_info=True)
            return f"Error listing wiki pages: {e}"

    @mcp.tool()
    async def get_wiki_page(
        page_name: str,
        server: str = DEFAULT_SERVER,
        container_path: str = DEFAULT_WIKI_CONTAINER,
    ) -> str:
        """Get full wiki page content and save to ai/.tmp/wiki-{page_name}.md.

        Wiki pages can contain HTML or wiki markup and may be large.
        Content is saved to a file for exploration with Grep/Read tools.

        Args:
            page_name: Wiki page name (e.g., 'tutorial_method_edit')
            server: LabKey server hostname (default: skyline.ms)
            container_path: Container path (default: /home/software/Skyline)

        Returns:
            Metadata about the page and file path. Use Read tool to view content.
        """
        try:
            server_context = get_server_context(server, container_path)

            result = labkey.query.select_rows(
                server_context=server_context,
                schema_name=WIKI_SCHEMA,
                query_name="wiki_page_content",
                max_rows=1,
                parameters={"PageName": page_name},
            )

            if not result or not result.get("rows"):
                return f"No wiki page found with name '{page_name}' in {container_path}"

            row = result["rows"][0]
            body = row.get("Body", "")
            title = row.get("Title", page_name)
            renderer = row.get("RendererType", "unknown")
            version = row.get("Version", "?")
            modified = row.get("Modified", "?")

            if not body:
                return f"Wiki page '{page_name}' exists but has no body content."

            # Build content for file
            lines = [
                f"# {title}",
                "",
                f"**Page name**: {page_name}",
                f"**Renderer**: {renderer}",
                f"**Version**: {version}",
                f"**Modified**: {modified}",
                "",
                "---",
                "",
                body,
            ]
            content = "\n".join(lines)

            # Determine output path
            output_dir = get_tmp_dir()

            # Sanitize page name for filename
            safe_name = page_name.replace("/", "_").replace("\\", "_").replace(" ", "_")
            output_file = output_dir / f"wiki-{safe_name}.md"
            output_file.write_text(content, encoding="utf-8")

            # Calculate metadata
            size_bytes = output_file.stat().st_size
            line_count = content.count("\n") + 1

            return (
                f"Wiki page saved successfully:\n"
                f"  file_path: {output_file}\n"
                f"  title: {title}\n"
                f"  renderer: {renderer}\n"
                f"  version: {version}\n"
                f"  modified: {modified}\n"
                f"  size_bytes: {size_bytes:,}\n"
                f"  line_count: {line_count:,}\n"
                f"\nUse Read tool to view content."
            )

        except Exception as e:
            logger.error(f"Error getting wiki page: {e}", exc_info=True)
            return f"Error getting wiki page: {e}"

    @mcp.tool()
    async def update_wiki_page(
        page_name: str,
        new_body: str,
        title: Optional[str] = None,
        server: str = DEFAULT_SERVER,
        container_path: str = DEFAULT_WIKI_CONTAINER,
    ) -> str:
        """Update an existing wiki page's content.

        CAUTION: This modifies live wiki content on skyline.ms.
        The wiki has version history, so changes can be reverted if needed.

        This tool:
        1. Retrieves current page metadata (entityId, rowId, pageVersionId)
        2. Encodes the new body using WAF format
        3. POSTs to wiki-saveWiki.view with proper CSRF headers

        Args:
            page_name: Wiki page name (e.g., 'tutorial_method_edit')
            new_body: The new page content (HTML or text depending on renderer)
            title: Optional new title for the page (if not provided, keeps existing title)
            server: LabKey server hostname (default: skyline.ms)
            container_path: Container path (default: /home/software/Skyline)

        Returns:
            Success message with version info, or error details.
        """
        try:
            # Step 1: Get current page metadata (also returns the session to reuse)
            logger.info(f"Getting metadata for wiki page: {page_name}")
            metadata, session = _get_wiki_page_metadata(page_name, server, container_path)

            if not metadata.get("entityId"):
                return f"Could not find wiki page '{page_name}' in {container_path}"

            old_version = metadata.get("pageVersionId", "?")
            logger.info(f"Current page version: {old_version}")

            # Step 2: Reuse the same session (same CSRF token) for the save

            # Step 3: Build the save request
            encoded_path = quote(container_path, safe="/")
            save_url = f"https://{server}{encoded_path}/wiki-saveWiki.view"

            # Normalize line endings to LF-only
            # LabKey adds its own CR before each LF, so sending CRLF results in CR CR LF
            normalized_body = new_body.replace("\r\n", "\n").replace("\r", "\n")

            # Encode the body using WAF format
            encoded_body = _encode_waf_body(normalized_body)

            # Use provided title or keep existing
            page_title = title if title is not None else metadata["title"]

            # Build payload matching the captured request
            payload = {
                "entityId": metadata["entityId"],
                "rowId": metadata["rowId"],
                "name": metadata["name"],
                "title": page_title,
                "body": encoded_body,
                "parent": metadata.get("parent", ""),
                "pageVersionId": metadata["pageVersionId"],
                "rendererType": metadata.get("rendererType", "HTML"),
                "webPartId": 0,
                "showAttachments": True,
                "shouldIndex": True,
                "isDirty": False,
                "useVisualEditor": False,
            }

            headers = {
                "X-Requested-With": "XMLHttpRequest",
                "Origin": f"https://{server}",
                "Referer": f"https://{server}{container_path}/wiki-edit.view?name={page_name}",
            }

            logger.info(f"Saving wiki page: {page_name}")
            status_code, result = session.post_json(save_url, payload, headers=headers)

            # Check response
            if status_code == 200:
                new_version = result.get("pageVersionId", "?")
                return (
                    f"Wiki page updated successfully:\n"
                    f"  page_name: {page_name}\n"
                    f"  container: {container_path}\n"
                    f"  old_version: {old_version}\n"
                    f"  new_version: {new_version}\n"
                    f"  title: {page_title}\n"
                    f"\nView at: https://{server}{container_path}/wiki-page.view?name={page_name}"
                )
            else:
                error_msg = result.get("error", str(result)[:500])
                return (
                    f"Wiki update failed with status {status_code}:\n"
                    f"  Response: {error_msg}"
                )

        except Exception as e:
            logger.error(f"Error updating wiki page: {e}", exc_info=True)
            return f"Error updating wiki page: {e}"

    @mcp.tool()
    async def list_wiki_attachments(
        page_name: str,
        server: str = DEFAULT_SERVER,
        container_path: str = DEFAULT_WIKI_CONTAINER,
    ) -> str:
        """List attachments for a wiki page.

        Gets the page's entityId and queries for attached files.
        Use get_wiki_attachment() to download a specific file.

        Args:
            page_name: Wiki page name (e.g., 'NewMachineBootstrap')
            server: LabKey server hostname (default: skyline.ms)
            container_path: Container path (default: /home/software/Skyline)

        Returns:
            List of attachments with filename, size, and type.
        """
        try:
            # Get entityId from wiki edit page
            metadata, _ = _get_wiki_page_metadata(page_name, server, container_path)
            entity_id = metadata.get("entityId")

            if not entity_id:
                return f"Could not find entityId for wiki page '{page_name}'"

            logger.info(f"Wiki page '{page_name}' has entityId: {entity_id}")

            # Query for attachments
            server_context = get_server_context(server, container_path)

            result = labkey.query.select_rows(
                server_context=server_context,
                schema_name="corex",
                query_name="documents_metadata",
                parameters={"ParentEntityId": entity_id},
                max_rows=100,
            )

            if not result or not result.get("rows"):
                return f"No attachments found for wiki page '{page_name}'"

            attachments = result["rows"]

            lines = [
                f"Found {len(attachments)} attachment(s) for wiki page '{page_name}':",
                f"  entityId: {entity_id}",
                "",
            ]

            for att in attachments:
                name = att.get("documentname", "?")
                size = att.get("documentsize", 0)
                doc_type = att.get("documenttype", "?")

                # Format size nicely
                if size > 1024 * 1024:
                    size_str = f"{size / (1024 * 1024):.1f} MB"
                elif size > 1024:
                    size_str = f"{size / 1024:.1f} KB"
                else:
                    size_str = f"{size} bytes"

                lines.append(f"  - {name} ({size_str}, {doc_type})")

            lines.extend([
                "",
                "To download an attachment:",
                f'  get_wiki_attachment("{page_name}", "filename")',
            ])

            return "\n".join(lines)

        except Exception as e:
            logger.error(f"Error listing wiki attachments: {e}", exc_info=True)
            return f"Error listing wiki attachments: {e}"

    @mcp.tool()
    async def get_wiki_attachment(
        page_name: str,
        filename: str,
        server: str = DEFAULT_SERVER,
        container_path: str = DEFAULT_WIKI_CONTAINER,
    ) -> str:
        """Download an attachment from a wiki page.

        For text files, returns content directly.
        For binary files (.png, .jpg, .pdf, etc.), saves to ai/.tmp/ and returns path.

        Args:
            page_name: Wiki page name (e.g., 'NewMachineBootstrap')
            filename: The attachment filename to download
            server: LabKey server hostname (default: skyline.ms)
            container_path: Container path (default: /home/software/Skyline)

        Returns:
            File content (for text files) or file path (for binary files).
        """
        import base64
        from pathlib import Path

        try:
            # Get entityId from wiki edit page
            metadata, _ = _get_wiki_page_metadata(page_name, server, container_path)
            entity_id = metadata.get("entityId")

            if not entity_id:
                return f"Could not find entityId for wiki page '{page_name}'"

            logger.info(f"Downloading '{filename}' from wiki page '{page_name}' (entityId: {entity_id})")

            # Build download URL (encode filename for spaces and special chars)
            login, password = get_netrc_credentials(server)
            encoded_filename = quote(filename, safe="")
            download_url = f"https://{server}{container_path}/wiki-download.view?entityId={entity_id}&name={encoded_filename}"

            # Create authenticated request
            request = urllib.request.Request(download_url)
            credentials = base64.b64encode(f"{login}:{password}".encode()).decode()
            request.add_header("Authorization", f"Basic {credentials}")

            with urllib.request.urlopen(request, timeout=60) as response:
                content = response.read()

            # Determine if text or binary based on extension
            text_extensions = {'.bat', '.py', '.txt', '.csv', '.xml', '.json', '.md', '.log', '.tsv', '.ini', '.cfg', '.yaml', '.yml', '.html', '.htm', '.css', '.js', '.sh', '.ps1', '.r', '.sql'}
            ext = Path(filename).suffix.lower()

            if ext in text_extensions:
                # Return text content directly
                try:
                    text_content = content.decode('utf-8')
                except UnicodeDecodeError:
                    try:
                        text_content = content.decode('latin-1')
                    except UnicodeDecodeError:
                        text_content = content.decode('utf-8', errors='replace')

                return (
                    f"**Attachment**: {filename}\n"
                    f"**Wiki page**: {page_name}\n"
                    f"**Size**: {len(content):,} bytes\n"
                    f"\n---\n\n"
                    f"{text_content}"
                )
            else:
                # Save binary file to ai/.tmp/attachments/
                output_dir = get_tmp_dir() / "attachments"
                output_dir.mkdir(parents=True, exist_ok=True)

                # Include page name prefix to avoid name collisions
                safe_page = page_name.replace("/", "_").replace("\\", "_")
                safe_filename = f"{safe_page}_{filename}"
                output_file = output_dir / safe_filename
                output_file.write_bytes(content)

                return (
                    f"Binary attachment saved:\n"
                    f"  wiki_page: {page_name}\n"
                    f"  filename: {filename}\n"
                    f"  size: {len(content):,} bytes\n"
                    f"  saved_to: {output_file}\n"
                    f"\nUse Read tool to view (for images) or appropriate application for other files."
                )

        except urllib.error.HTTPError as e:
            return f"HTTP Error {e.code}: {e.reason}. Check that the page name and filename are correct."
        except Exception as e:
            logger.error(f"Error downloading wiki attachment: {e}", exc_info=True)
            return f"Error downloading wiki attachment: {e}"
