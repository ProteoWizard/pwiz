"""Support board tools for LabKey MCP server.

This module contains tools for querying and reading support board threads
from the skyline.ms support forum.
"""

import logging
from datetime import datetime

import labkey

from .common import (
    get_server_context,
    get_tmp_dir,
    DEFAULT_SERVER,
    DEFAULT_SUPPORT_CONTAINER,
    ANNOUNCEMENT_SCHEMA_SUPPORT,
)

logger = logging.getLogger("labkey_mcp")


def register_tools(mcp):
    """Register support board tools."""

    @mcp.tool()
    async def query_support_threads(
        days: int = 30,
        max_rows: int = 50,
        server: str = DEFAULT_SERVER,
        container_path: str = DEFAULT_SUPPORT_CONTAINER,
    ) -> str:
        """Query recent support board threads.

        Returns thread summaries including title, creation date, and response count.
        Use get_support_thread() to retrieve full thread with all posts.

        Args:
            days: Number of days back to query (default: 30)
            max_rows: Maximum threads to return (default: 50)
            server: LabKey server hostname (default: skyline.ms)
            container_path: Container path (default: /home/support)
        """
        try:
            server_context = get_server_context(server, container_path)

            result = labkey.query.select_rows(
                server_context=server_context,
                schema_name=ANNOUNCEMENT_SCHEMA_SUPPORT,
                query_name="announcement_threads_recent",
                max_rows=max_rows,
                parameters={"DaysBack": str(days)},
                sort="-Created",  # API sort required - ORDER BY in SQL unreliable
            )

            if result and result.get("rows"):
                rows = result["rows"]
                total = result.get("rowCount", len(rows))

                lines = [
                    f"Found {total} threads in last {days} days (showing {len(rows)}):",
                    "",
                    "| RowId | Title | Created | Responses |",
                    "|-------|-------|---------|-----------|",
                ]

                for row in rows:
                    row_id = row.get("RowId", "?")
                    title = row.get("Title", "?")[:50]
                    created = str(row.get("Created", "?"))[:10]
                    responses = row.get("ResponseCount", 0)
                    lines.append(f"| {row_id} | {title} | {created} | {responses} |")

                lines.extend([
                    "",
                    "Use get_support_thread(thread_id) to view full thread with all posts.",
                ])

                return "\n".join(lines)
            return f"No threads found in the last {days} days."

        except Exception as e:
            logger.error(f"Error querying support threads: {e}", exc_info=True)
            return f"Error querying support threads: {e}"

    @mcp.tool()
    async def get_support_thread(
        thread_id: int,
        server: str = DEFAULT_SERVER,
        container_path: str = DEFAULT_SUPPORT_CONTAINER,
    ) -> str:
        """Get full support thread with all posts and save to ai/.tmp/support-thread-{id}.md.

        Thread posts can be lengthy and contain code samples, data examples, etc.
        Content is saved to a file for exploration with Grep/Read tools.

        Args:
            thread_id: The RowId of the thread to retrieve
            server: LabKey server hostname (default: skyline.ms)
            container_path: Container path (default: /home/support)

        Returns:
            Metadata about the thread and file path. Use Read tool to view content.
        """
        try:
            server_context = get_server_context(server, container_path)

            result = labkey.query.select_rows(
                server_context=server_context,
                schema_name=ANNOUNCEMENT_SCHEMA_SUPPORT,
                query_name="announcement_thread_posts",
                max_rows=200,
                parameters={"ThreadId": str(thread_id)},
                sort="Created",  # Chronological order for reading thread
            )

            if not result or not result.get("rows"):
                return f"No thread found with RowId={thread_id}"

            rows = result["rows"]

            # First post is the thread starter
            first_post = rows[0]
            thread_title = first_post.get("Title", f"Thread {thread_id}")

            # Build content for file
            lines = [
                f"# {thread_title}",
                "",
                f"**Thread ID**: {thread_id}",
                f"**Posts**: {len(rows)}",
                "",
                "---",
                "",
            ]

            for i, row in enumerate(rows):
                post_id = row.get("RowId", "?")
                entity_id = row.get("EntityId", "")
                title = row.get("Title", "")
                body = row.get("FormattedBody", "")  # FormattedBody contains HTML content
                created = row.get("Created", "?")
                created_by = row.get("CreatedBy", "?")

                if i == 0:
                    lines.append("## Original Post")
                else:
                    lines.append(f"## Reply #{i}")

                lines.extend([
                    "",
                    f"**From**: {created_by}",
                    f"**Date**: {created}",
                ])

                # Include EntityId for attachment lookups
                if entity_id:
                    lines.append(f"**EntityId**: {entity_id}")

                lines.append("")

                if title and i > 0:  # Show title for replies if different
                    lines.append(f"**Subject**: {title}")
                    lines.append("")

                lines.append(body if body else "(no content)")
                lines.extend(["", "---", ""])

            content = "\n".join(lines)

            # Determine output path
            output_dir = get_tmp_dir()

            output_file = output_dir / f"support-thread-{thread_id}.md"
            output_file.write_text(content, encoding="utf-8")

            # Calculate metadata
            size_bytes = output_file.stat().st_size
            line_count = content.count("\n") + 1

            return (
                f"Support thread saved successfully:\n"
                f"  file_path: {output_file}\n"
                f"  title: {thread_title}\n"
                f"  posts: {len(rows)}\n"
                f"  size_bytes: {size_bytes:,}\n"
                f"  line_count: {line_count:,}\n"
                f"\nUse Read tool to view content."
            )

        except Exception as e:
            logger.error(f"Error getting support thread: {e}", exc_info=True)
            return f"Error getting support thread: {e}"

    @mcp.tool()
    async def get_support_summary(
        days: int = 1,
        server: str = DEFAULT_SERVER,
        container_path: str = DEFAULT_SUPPORT_CONTAINER,
    ) -> str:
        """Generate a summary of support board activity and save to ai/.tmp/support-report-YYYYMMDD.md.

        Similar to get_daily_test_summary, this provides an overview of support board
        activity for a given time period. Useful for daily or weekly review.

        Args:
            days: Number of days back to query (default: 1)
            server: LabKey server hostname (default: skyline.ms)
            container_path: Container path (default: /home/support)

        Returns:
            Brief summary with file path. Full details are in the saved file.
        """
        try:
            server_context = get_server_context(server, container_path)

            # Query recent threads
            result = labkey.query.select_rows(
                server_context=server_context,
                schema_name=ANNOUNCEMENT_SCHEMA_SUPPORT,
                query_name="announcement_threads_recent",
                max_rows=200,
                parameters={"DaysBack": str(days)},
                sort="-Created",  # API sort required - ORDER BY in SQL unreliable
            )

            if not result or not result.get("rows"):
                return f"No support threads found in the last {days} day(s)."

            threads = result["rows"]

            # Categorize threads
            new_threads = []  # No responses yet
            active_threads = []  # Has responses

            for thread in threads:
                row_id = thread.get("RowId", "?")
                title = thread.get("Title", "?")
                created = thread.get("Created", "?")
                created_by = thread.get("CreatedBy", "?")
                response_count = thread.get("ResponseCount", 0)

                thread_info = {
                    "row_id": row_id,
                    "title": title,
                    "created": str(created)[:16] if created else "?",
                    "created_by": created_by,
                    "responses": response_count,
                }

                if response_count == 0:
                    new_threads.append(thread_info)
                else:
                    active_threads.append(thread_info)

            # Build report
            report_date = datetime.now().strftime("%Y-%m-%d")
            lines = [
                f"# Support Board Summary",
                f"",
                f"**Period**: Last {days} day(s) (as of {report_date})",
                f"**Total threads**: {len(threads)}",
                f"",
                "## Summary",
                "",
                f"| Category | Count |",
                f"|----------|-------|",
                f"| New (unanswered) | {len(new_threads)} |",
                f"| Active (has responses) | {len(active_threads)} |",
                "",
            ]

            # New/unanswered threads - these need attention
            if new_threads:
                lines.extend([
                    "## Unanswered Threads (Need Response)",
                    "",
                    "| ID | Title | Posted | By |",
                    "|----|-------|--------|-----|",
                ])
                for t in new_threads:
                    title_short = t["title"][:60] + "..." if len(t["title"]) > 60 else t["title"]
                    lines.append(f"| {t['row_id']} | {title_short} | {t['created']} | {t['created_by']} |")
                lines.append("")

            # Active threads with responses
            if active_threads:
                lines.extend([
                    "## Active Threads (Has Responses)",
                    "",
                    "| ID | Title | Posted | Responses |",
                    "|----|-------|--------|-----------|",
                ])
                for t in active_threads:
                    title_short = t["title"][:60] + "..." if len(t["title"]) > 60 else t["title"]
                    lines.append(f"| {t['row_id']} | {title_short} | {t['created']} | {t['responses']} |")
                lines.append("")

            # Add instructions
            lines.extend([
                "## Next Steps",
                "",
                "To view a specific thread:",
                "```",
                "get_support_thread(thread_id)",
                "```",
                "",
                "Thread content will be saved to `ai/.tmp/support-thread-{id}.md`",
                "",
            ])

            # Write report to file
            report_content = "\n".join(lines)

            output_dir = get_tmp_dir()

            date_str = datetime.now().strftime("%Y%m%d")
            output_file = output_dir / f"support-report-{date_str}.md"
            output_file.write_text(report_content, encoding="utf-8")

            # Build brief summary
            brief = [
                f"Support board report saved to: {output_file}",
                "",
                f"Summary for last {days} day(s):",
                f"  - Total threads: {len(threads)}",
                f"  - Unanswered (need response): {len(new_threads)}",
                f"  - Active (has responses): {len(active_threads)}",
                "",
            ]

            if new_threads:
                brief.append("Unanswered threads to review:")
                for t in new_threads[:5]:  # Show first 5
                    brief.append(f"  - [{t['row_id']}] {t['title'][:50]}")
                if len(new_threads) > 5:
                    brief.append(f"  ... and {len(new_threads) - 5} more")

            brief.append("")
            brief.append(f"See {output_file} for full details.")

            return "\n".join(brief)

        except Exception as e:
            logger.error(f"Error generating support summary: {e}", exc_info=True)
            return f"Error generating support summary: {e}"
