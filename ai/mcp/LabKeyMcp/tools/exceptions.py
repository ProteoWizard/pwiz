"""Exception triage tools for LabKey MCP server.

This module contains tools for querying Skyline exception reports
from the skyline.ms exception tracking system.
"""

import logging
from datetime import datetime, timedelta

import labkey
from labkey.query import QueryFilter

from .common import (
    get_server_context,
    get_tmp_dir,
    DEFAULT_SERVER,
    DEFAULT_CONTAINER,
    EXCEPTION_SCHEMA,
    EXCEPTION_QUERY,
)

logger = logging.getLogger("labkey_mcp")


def register_tools(mcp):
    """Register exception triage tools."""

    @mcp.tool()
    async def query_exceptions(
        days: int = 7,
        max_rows: int = 50,
        server: str = DEFAULT_SERVER,
        container_path: str = DEFAULT_CONTAINER,
    ) -> str:
        """**DRILL-DOWN**: Browse recent exceptions. Prefer save_exceptions_report for daily review.

        Args:
            days: Days back to query (default: 7)
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
        """**DRILL-DOWN**: Full stack trace for one exception.

        Args:
            exception_id: RowId from save_exceptions_report
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

    @mcp.tool()
    async def save_exceptions_report(
        report_date: str,
        server: str = DEFAULT_SERVER,
        container_path: str = DEFAULT_CONTAINER,
    ) -> str:
        """**PRIMARY**: Daily exception report with full stack traces. Saves to ai/.tmp/exceptions-report-YYYYMMDD.md.

        Args:
            report_date: Date YYYY-MM-DD
        """
        try:
            # Parse report_date for the 24-hour window
            date_obj = datetime.strptime(report_date, "%Y-%m-%d")
            next_day = date_obj + timedelta(days=1)

            # Filter from start of day to start of next day
            start_date = date_obj.strftime("%Y-%m-%d")
            end_date = next_day.strftime("%Y-%m-%d")

            server_context = get_server_context(server, container_path)

            # Query exceptions created on the report date
            filter_array = [
                QueryFilter("Created", start_date, "dategte"),
                QueryFilter("Created", end_date, "datelt"),
            ]

            result = labkey.query.select_rows(
                server_context=server_context,
                schema_name=EXCEPTION_SCHEMA,
                query_name=EXCEPTION_QUERY,
                max_rows=500,
                sort="-Created",
                filter_array=filter_array,
            )

            if not result or not result.get("rows"):
                return f"No exceptions found for {report_date}."

            rows = result["rows"]

            # Build the report
            lines = [
                f"# Exception Report: {report_date}",
                "",
                f"**Total Exceptions**: {len(rows)}",
                "",
            ]

            # Summary table
            lines.append("## Summary")
            lines.append("")
            lines.append("| # | RowId | Title | Status | Created |")
            lines.append("|---|-------|-------|--------|---------|")

            for i, row in enumerate(rows, 1):
                row_id = row.get("RowId", "?")
                title = row.get("Title", "Unknown")[:50]
                if len(row.get("Title", "")) > 50:
                    title += "..."
                status = row.get("Status") or "Unassigned"
                created = row.get("Created", "?")
                if isinstance(created, str) and "T" in created:
                    created = created.split("T")[1][:8] if "T" in created else created

                lines.append(f"| {i} | {row_id} | {title} | {status} | {created} |")

            lines.append("")
            lines.append("---")
            lines.append("")

            # Full details for each exception
            lines.append("## Full Exception Details")
            lines.append("")

            for i, row in enumerate(rows, 1):
                row_id = row.get("RowId", "?")
                title = row.get("Title", "Unknown")
                created = row.get("Created", "Unknown")
                modified = row.get("Modified", "Unknown")
                status = row.get("Status") or "Unassigned"
                assigned_to = row.get("AssignedTo") or "Nobody"
                body = row.get("FormattedBody", "No content")

                lines.append(f"### Exception #{row_id}: {title}")
                lines.append("")
                lines.append(f"- **Created**: {created}")
                lines.append(f"- **Modified**: {modified}")
                lines.append(f"- **Status**: {status}")
                lines.append(f"- **Assigned To**: {assigned_to}")
                lines.append("")
                lines.append("**Full Report:**")
                lines.append("```")
                lines.append(body)
                lines.append("```")
                lines.append("")
                lines.append("---")
                lines.append("")

            # Save to file
            content = "\n".join(lines)
            date_str = date_obj.strftime("%Y%m%d")
            file_path = get_tmp_dir() / f"exceptions-report-{date_str}.md"
            file_path.write_text(content, encoding="utf-8")

            return (
                f"Saved {len(rows)} exception(s) to {file_path}\n\n"
                f"Report for: {report_date}\n"
                f"Use Read tool to view full details."
            )

        except Exception as e:
            logger.error(f"Error generating exceptions report: {e}", exc_info=True)
            return f"Error generating exceptions report: {e}"
