"""Attachment tools for LabKey MCP server.

This module contains tools for listing and downloading attachments
from support board posts and wiki pages on skyline.ms.
"""

import logging
import urllib.error
import urllib.request
from pathlib import Path

import labkey

from .common import (
    get_server_context,
    get_netrc_credentials,
    get_tmp_dir,
    DEFAULT_SERVER,
    DEFAULT_SUPPORT_CONTAINER,
)

logger = logging.getLogger("labkey_mcp")


def register_tools(mcp):
    """Register attachment tools."""

    @mcp.tool()
    async def list_attachments(
        parent_entity_id: str,
        server: str = DEFAULT_SERVER,
        container_path: str = DEFAULT_SUPPORT_CONTAINER,
    ) -> str:
        """List attachments for a support post or wiki page.

        Queries the corex.documents table to find attachments linked to a parent entity.
        Does NOT retrieve file contents - use get_attachment() for that.

        Args:
            parent_entity_id: The EntityId of the parent object (announcement post or wiki page)
            server: LabKey server hostname (default: skyline.ms)
            container_path: Container path (default: /home/support)

        Returns:
            List of attachments with filename, size, and type.
        """
        try:
            server_context = get_server_context(server, container_path)

            # Query documents_metadata (custom query that excludes binary document column)
            result = labkey.query.select_rows(
                server_context=server_context,
                schema_name="corex",
                query_name="documents_metadata",
                parameters={"ParentEntityId": parent_entity_id},
                max_rows=100,
            )

            if not result or not result.get("rows"):
                return f"No attachments found for entity: {parent_entity_id}"

            attachments = result["rows"]

            lines = [
                f"Found {len(attachments)} attachment(s) for entity {parent_entity_id}:",
                "",
            ]

            for att in attachments:
                name = att.get("documentname", "?")
                size = att.get("documentsize", 0)
                doc_type = att.get("documenttype", "?")
                created = str(att.get("created", "?"))[:16]

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
                f'  get_attachment("{parent_entity_id}", "filename")',
            ])

            return "\n".join(lines)

        except Exception as e:
            logger.error(f"Error listing attachments: {e}", exc_info=True)
            return f"Error listing attachments: {e}"

    @mcp.tool()
    async def get_attachment(
        parent_entity_id: str,
        filename: str,
        server: str = DEFAULT_SERVER,
        container_path: str = DEFAULT_SUPPORT_CONTAINER,
    ) -> str:
        """Download an attachment from a support post or wiki page.

        For text files (.bat, .py, .txt, .csv, .xml, .json, .md, .log), returns content directly.
        For binary files (.png, .jpg, .sky, .skyd, .pdf), saves to ai/.tmp/ and returns path.

        Args:
            parent_entity_id: The EntityId of the parent object
            filename: The attachment filename to download
            server: LabKey server hostname (default: skyline.ms)
            container_path: Container path (default: /home/support)

        Returns:
            File content (for text files) or file path (for binary files).
        """
        import base64

        try:
            login, password = get_netrc_credentials(server)

            # Build download URL
            # For support: announcements-download.view
            # For wiki: wiki-download.view (uses different parameter names)
            if "support" in container_path.lower():
                download_url = f"https://{server}{container_path}/announcements-download.view?entityId={parent_entity_id}&name={filename}"
            else:
                download_url = f"https://{server}{container_path}/wiki-download.view?entityId={parent_entity_id}&name={filename}"

            # Create authenticated request
            request = urllib.request.Request(download_url)
            credentials = base64.b64encode(f"{login}:{password}".encode()).decode()
            request.add_header("Authorization", f"Basic {credentials}")

            logger.info(f"Downloading attachment: {filename}")

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
                    f"**Size**: {len(content):,} bytes\n"
                    f"\n---\n\n"
                    f"{text_content}"
                )
            else:
                # Save binary file to ai/.tmp/
                output_dir = get_tmp_dir() / "attachments"
                output_dir.mkdir(parents=True, exist_ok=True)

                # Include entity ID prefix to avoid name collisions
                safe_filename = f"{parent_entity_id[:8]}_{filename}"
                output_file = output_dir / safe_filename
                output_file.write_bytes(content)

                return (
                    f"Binary attachment saved:\n"
                    f"  filename: {filename}\n"
                    f"  size: {len(content):,} bytes\n"
                    f"  saved_to: {output_file}\n"
                    f"\nUse Read tool to view (for images) or appropriate application for other files."
                )

        except urllib.error.HTTPError as e:
            return f"HTTP Error {e.code}: {e.reason}. Check that the entityId and filename are correct."
        except Exception as e:
            logger.error(f"Error downloading attachment: {e}", exc_info=True)
            return f"Error downloading attachment: {e}"
