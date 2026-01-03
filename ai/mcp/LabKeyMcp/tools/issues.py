"""Issue tracking tools for LabKey MCP server.

This module contains tools for querying and reading issues from the
skyline.ms issue tracker at /home/issues.
"""

import logging
from datetime import datetime
from typing import Optional

import labkey

from .common import (
    get_server_context,
    get_tmp_dir,
    DEFAULT_SERVER,
    DEFAULT_ISSUES_CONTAINER,
    ISSUES_SCHEMA,
)

logger = logging.getLogger("labkey_mcp")


def register_tools(mcp):
    """Register issue tracking tools."""

    @mcp.tool()
    async def query_issues(
        status: Optional[str] = None,
        issue_type: Optional[str] = None,
        max_rows: int = 50,
        server: str = DEFAULT_SERVER,
        container_path: str = DEFAULT_ISSUES_CONTAINER,
    ) -> str:
        """**DRILL-DOWN**: Browse issues. Prefer save_issues_report for overview.

        Args:
            status: 'open' or 'closed' (default: all)
            issue_type: 'Defect' or 'Todo' (default: all)
        """
        try:
            server_context = get_server_context(server, container_path)

            # Use issues_by_status for server-side filtering when status specified
            if status:
                # Use parameterized query with wide date range for server-side filtering
                result = labkey.query.select_rows(
                    server_context=server_context,
                    schema_name=ISSUES_SCHEMA,
                    query_name="issues_by_status",
                    parameters={
                        "Status": status,
                        "StartDate": "1990-01-01",
                        "EndDate": "2099-12-31",
                    },
                    max_rows=max_rows if not issue_type else max_rows * 10,
                    sort="-Modified",  # API sort required - ORDER BY in SQL unreliable
                )
            else:
                # No status filter - use issues_list
                result = labkey.query.select_rows(
                    server_context=server_context,
                    schema_name=ISSUES_SCHEMA,
                    query_name="issues_list",
                    max_rows=max_rows if not issue_type else max_rows * 10,
                    sort="-Modified",  # API sort required - ORDER BY in SQL unreliable
                )

            if not result or not result.get("rows"):
                return "No issues found."

            rows = result["rows"]

            # Apply type filter client-side (status already filtered server-side if specified)
            if issue_type:
                rows = [r for r in rows if r.get("Type", "").lower() == issue_type.lower()]

            # Limit to max_rows after filtering
            rows = rows[:max_rows]

            if not rows:
                filters = []
                if status:
                    filters.append(f"status={status}")
                if issue_type:
                    filters.append(f"type={issue_type}")
                return f"No issues found matching filters: {', '.join(filters)}"

            total = result.get("rowCount", len(result["rows"]))

            lines = [
                f"Found {len(rows)} issues (of {total} total):",
                "",
                "| ID | Title | Status | Type | Priority | Assigned | Modified |",
                "|----|-------|--------|------|----------|----------|----------|",
            ]

            for row in rows:
                issue_id = row.get("IssueId", "?")
                title = row.get("Title", "?")
                if len(title) > 50:
                    title = title[:47] + "..."
                status_val = row.get("Status", "?")
                type_val = row.get("Type", "?")
                priority = row.get("Priority", "?")
                assigned = row.get("AssignedTo", "-") or "-"
                modified = str(row.get("Modified", "?"))[:10]

                lines.append(
                    f"| {issue_id} | {title} | {status_val} | {type_val} | {priority} | {assigned} | {modified} |"
                )

            lines.extend([
                "",
                "Use get_issue_details(issue_id) to view full issue with comments.",
                "Use list_attachments(entity_id, container_path='/home/issues') for attachments.",
            ])

            return "\n".join(lines)

        except Exception as e:
            logger.error(f"Error querying issues: {e}", exc_info=True)
            return f"Error querying issues: {e}"

    @mcp.tool()
    async def get_issue_details(
        issue_id: int,
        server: str = DEFAULT_SERVER,
        container_path: str = DEFAULT_ISSUES_CONTAINER,
    ) -> str:
        """**DRILL-DOWN**: Full issue with comments. Saves to ai/.tmp/issue-{id}.md.

        Args:
            issue_id: IssueId from query_issues
        """
        try:
            server_context = get_server_context(server, container_path)

            # Use issue_with_comments parameterized query
            result = labkey.query.select_rows(
                server_context=server_context,
                schema_name=ISSUES_SCHEMA,
                query_name="issue_with_comments",
                parameters={"IssueId": str(issue_id)},
                max_rows=500,  # Enough for extensive comment threads
            )

            if not result or not result.get("rows"):
                return f"No issue found with IssueId={issue_id}"

            rows = result["rows"]

            # First row has issue metadata (may be repeated if multiple comments)
            first_row = rows[0]
            title = first_row.get("Title", f"Issue {issue_id}")
            status = first_row.get("Status", "?")
            issue_type = first_row.get("Type", "?")
            area = first_row.get("Area", "?")
            priority = first_row.get("Priority", "?")
            milestone = first_row.get("Milestone", "-") or "-"
            resolution = first_row.get("Resolution", "-") or "-"
            created = first_row.get("Created", "?")
            modified = first_row.get("Modified", "?")
            resolved = first_row.get("Resolved", "-") or "-"
            closed = first_row.get("Closed", "-") or "-"
            assigned_to = first_row.get("AssignedTo", "-") or "-"
            created_by = first_row.get("CreatedBy", "?")
            resolved_by = first_row.get("ResolvedBy", "-") or "-"
            closed_by = first_row.get("ClosedBy", "-") or "-"
            entity_id = first_row.get("EntityId", "")

            # Build content for file
            lines = [
                f"# Issue {issue_id}: {title}",
                "",
                "## Metadata",
                "",
                f"| Field | Value |",
                f"|-------|-------|",
                f"| **Status** | {status} |",
                f"| **Type** | {issue_type} |",
                f"| **Area** | {area} |",
                f"| **Priority** | {priority} |",
                f"| **Milestone** | {milestone} |",
                f"| **Resolution** | {resolution} |",
                f"| **Assigned To** | {assigned_to} |",
                f"| **Created** | {created} |",
                f"| **Created By** | {created_by} |",
                f"| **Modified** | {modified} |",
                f"| **Resolved** | {resolved} |",
                f"| **Resolved By** | {resolved_by} |",
                f"| **Closed** | {closed} |",
                f"| **Closed By** | {closed_by} |",
            ]

            if entity_id:
                lines.append(f"| **EntityId** | {entity_id} |")

            lines.extend(["", "---", ""])

            # Extract unique comments (the join may duplicate issue fields)
            seen_comments = set()
            comments = []
            for row in rows:
                comment_id = row.get("CommentId")
                if comment_id and comment_id not in seen_comments:
                    seen_comments.add(comment_id)
                    comments.append({
                        "id": comment_id,
                        "created": row.get("CommentCreated", "?"),
                        "by": row.get("CommentBy", "?"),
                        "text": row.get("Comment", ""),
                    })

            if comments:
                lines.append(f"## Comments ({len(comments)})")
                lines.append("")

                for i, comment in enumerate(comments, 1):
                    lines.extend([
                        f"### Comment {i}",
                        "",
                        f"**By**: {comment['by']}",
                        f"**Date**: {comment['created']}",
                        "",
                        comment["text"] if comment["text"] else "(empty comment)",
                        "",
                        "---",
                        "",
                    ])
            else:
                lines.extend([
                    "## Comments",
                    "",
                    "(No comments on this issue)",
                    "",
                ])

            # Add attachment info
            if entity_id:
                lines.extend([
                    "## Attachments",
                    "",
                    "To check for attachments:",
                    "```",
                    f'list_attachments("{entity_id}", container_path="/home/issues")',
                    "```",
                    "",
                ])

            content = "\n".join(lines)

            # Write to file
            output_dir = get_tmp_dir()
            output_file = output_dir / f"issue-{issue_id}.md"
            output_file.write_text(content, encoding="utf-8")

            # Calculate metadata
            size_bytes = output_file.stat().st_size
            line_count = content.count("\n") + 1

            return (
                f"Issue details saved successfully:\n"
                f"  file_path: {output_file}\n"
                f"  issue_id: {issue_id}\n"
                f"  title: {title}\n"
                f"  status: {status}\n"
                f"  type: {issue_type}\n"
                f"  comments: {len(comments)}\n"
                f"  size_bytes: {size_bytes:,}\n"
                f"  line_count: {line_count:,}\n"
                f"\nUse Read tool to view content."
            )

        except Exception as e:
            logger.error(f"Error getting issue details: {e}", exc_info=True)
            return f"Error getting issue details: {e}"

    @mcp.tool()
    async def save_issues_report(
        status: str = "open",
        server: str = DEFAULT_SERVER,
        container_path: str = DEFAULT_ISSUES_CONTAINER,
    ) -> str:
        """**PRIMARY**: Issue tracker summary. Saves to ai/.tmp/issues-report-{status}-YYYYMMDD.md.

        Args:
            status: 'open' or 'closed' (default: 'open')
        """
        try:
            server_context = get_server_context(server, container_path)

            # Query all issues with the specified status
            result = labkey.query.select_rows(
                server_context=server_context,
                schema_name=ISSUES_SCHEMA,
                query_name="issues_list",
                max_rows=1000,
            )

            if not result or not result.get("rows"):
                return f"No issues found."

            rows = result["rows"]

            # Filter by status
            issues = [r for r in rows if r.get("Status", "").lower() == status.lower()]

            if not issues:
                return f"No {status} issues found."

            # Categorize by type
            defects = [i for i in issues if i.get("Type", "").lower() == "defect"]
            todos = [i for i in issues if i.get("Type", "").lower() == "todo"]
            other = [i for i in issues if i not in defects and i not in todos]

            # Categorize by priority
            by_priority = {}
            for issue in issues:
                p = issue.get("Priority", "?")
                by_priority.setdefault(p, []).append(issue)

            # Categorize by area
            by_area = {}
            for issue in issues:
                area = issue.get("Area", "-") or "-"
                by_area.setdefault(area, []).append(issue)

            # Categorize by milestone
            by_milestone = {}
            for issue in issues:
                milestone = issue.get("Milestone", "-") or "-"
                by_milestone.setdefault(milestone, []).append(issue)

            # Categorize by assignee
            by_assignee = {}
            for issue in issues:
                assignee = issue.get("AssignedTo", "-") or "-"
                by_assignee.setdefault(assignee, []).append(issue)

            # Age analysis (years since last modified)
            from datetime import datetime as dt
            now = dt.now()
            age_buckets = {"< 1 year": 0, "1-2 years": 0, "2-3 years": 0, "3-5 years": 0, "> 5 years": 0}
            for issue in issues:
                mod = issue.get("Modified", "")
                if mod:
                    try:
                        mod_date = dt.fromisoformat(mod.replace("Z", "+00:00").split("+")[0])
                        years = (now - mod_date).days / 365
                        if years < 1:
                            age_buckets["< 1 year"] += 1
                        elif years < 2:
                            age_buckets["1-2 years"] += 1
                        elif years < 3:
                            age_buckets["2-3 years"] += 1
                        elif years < 5:
                            age_buckets["3-5 years"] += 1
                        else:
                            age_buckets["> 5 years"] += 1
                    except:
                        pass

            # Build report
            report_date = datetime.now().strftime("%Y-%m-%d")
            lines = [
                f"# {status.capitalize()} Issues Report",
                "",
                f"**Generated**: {report_date}",
                f"**Total {status} issues**: {len(issues)}",
                "",
                "## Summary by Type",
                "",
                "| Type | Count |",
                "|------|-------|",
                f"| Defect | {len(defects)} |",
                f"| Todo | {len(todos)} |",
            ]
            if other:
                lines.append(f"| Other | {len(other)} |")

            lines.extend([
                "",
                "## Summary by Priority",
                "",
                "| Priority | Count |",
                "|----------|-------|",
            ])
            for p in sorted(by_priority.keys()):
                lines.append(f"| {p} | {len(by_priority[p])} |")

            # Summary by Area
            lines.extend([
                "",
                "## Summary by Area",
                "",
                "| Area | Count |",
                "|------|-------|",
            ])
            for area in sorted(by_area.keys(), key=lambda x: -len(by_area[x])):
                lines.append(f"| {area} | {len(by_area[area])} |")

            # Summary by Assignee
            lines.extend([
                "",
                "## Summary by Assignee",
                "",
                "| Assignee | Count |",
                "|----------|-------|",
            ])
            for assignee in sorted(by_assignee.keys(), key=lambda x: -len(by_assignee[x])):
                lines.append(f"| {assignee} | {len(by_assignee[assignee])} |")

            # Summary by Milestone
            if any(m != "-" for m in by_milestone.keys()):
                lines.extend([
                    "",
                    "## Summary by Milestone",
                    "",
                    "| Milestone | Count |",
                    "|-----------|-------|",
                ])
                for milestone in sorted(by_milestone.keys(), key=lambda x: -len(by_milestone[x])):
                    lines.append(f"| {milestone} | {len(by_milestone[milestone])} |")

            # Age Analysis
            lines.extend([
                "",
                "## Age Analysis (time since last modified)",
                "",
                "| Age | Count |",
                "|-----|-------|",
            ])
            for age in ["< 1 year", "1-2 years", "2-3 years", "3-5 years", "> 5 years"]:
                if age_buckets[age] > 0:
                    lines.append(f"| {age} | {age_buckets[age]} |")

            # List defects
            if defects:
                lines.extend([
                    "",
                    "## Defects",
                    "",
                    "| ID | Title | Priority | Assigned | Modified |",
                    "|----|-------|----------|----------|----------|",
                ])
                for issue in sorted(defects, key=lambda x: (x.get("Priority", 99), x.get("IssueId", 0))):
                    issue_id = issue.get("IssueId", "?")
                    title = issue.get("Title", "?")
                    if len(title) > 60:
                        title = title[:57] + "..."
                    priority = issue.get("Priority", "?")
                    assigned = issue.get("AssignedTo", "-") or "-"
                    modified = str(issue.get("Modified", "?"))[:10]
                    lines.append(f"| {issue_id} | {title} | {priority} | {assigned} | {modified} |")

            # List todos
            if todos:
                lines.extend([
                    "",
                    "## TODOs",
                    "",
                    "| ID | Title | Priority | Assigned | Modified |",
                    "|----|-------|----------|----------|----------|",
                ])
                for issue in sorted(todos, key=lambda x: (x.get("Priority", 99), x.get("IssueId", 0))):
                    issue_id = issue.get("IssueId", "?")
                    title = issue.get("Title", "?")
                    if len(title) > 60:
                        title = title[:57] + "..."
                    priority = issue.get("Priority", "?")
                    assigned = issue.get("AssignedTo", "-") or "-"
                    modified = str(issue.get("Modified", "?"))[:10]
                    lines.append(f"| {issue_id} | {title} | {priority} | {assigned} | {modified} |")

            # Add usage instructions
            lines.extend([
                "",
                "## Next Steps",
                "",
                "To view full issue with comments:",
                "```",
                "get_issue_details(issue_id)",
                "```",
                "",
                "To check for attachments:",
                "```",
                'list_attachments(entity_id, container_path="/home/issues")',
                "```",
                "",
            ])

            # Write report to file
            report_content = "\n".join(lines)
            output_dir = get_tmp_dir()
            date_str = datetime.now().strftime("%Y%m%d")
            output_file = output_dir / f"issues-report-{status}-{date_str}.md"
            output_file.write_text(report_content, encoding="utf-8")

            # Build brief summary
            return (
                f"Issues report saved to: {output_file}\n"
                f"\n"
                f"Summary of {status} issues:\n"
                f"  - Total: {len(issues)}\n"
                f"  - Defects: {len(defects)}\n"
                f"  - TODOs: {len(todos)}\n"
                f"\n"
                f"See {output_file} for full details."
            )

        except Exception as e:
            logger.error(f"Error generating issues report: {e}", exc_info=True)
            return f"Error generating issues report: {e}"
