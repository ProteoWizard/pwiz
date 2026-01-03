"""Exception triage tools for LabKey MCP server.

This module contains tools for querying Skyline exception reports
from the skyline.ms exception tracking system.

Enhanced with stack trace normalization (2025-12-31) to:
- Group exceptions by fingerprint (same bug = same fingerprint)
- Track unique users via Installation ID (dedupe frustrated users)
- Track software versions for known-fix correlation
"""

import logging
import re
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
from .stacktrace import normalize_stack_trace

logger = logging.getLogger("labkey_mcp")

# Patterns for parsing exception body
INSTALLATION_ID_PATTERN = re.compile(
    r'Installation ID:\s*([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})'
)
VERSION_PATTERN = re.compile(
    r'Skyline version:\s*(\d+\.\d+\.\d+\.\d+(?:-[0-9a-fA-F]+)?)\s*\((\d+-bit)\)'
)
# Email pattern - users sometimes provide contact info
EMAIL_PATTERN = re.compile(
    r'[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}'
)
STACK_TRACE_SEPARATOR = '--------------------'

# History settings
HISTORY_FILE = 'exception-history.json'
RETENTION_MONTHS = 9  # Cover full release cycle + buffer

# Current major release anchor for backfill
MAJOR_RELEASE_VERSION = "25.1"
MAJOR_RELEASE_DATE = "2025-05-22"


def _parse_exception_body(body: str) -> dict:
    """Parse structured data from exception FormattedBody.

    Returns dict with:
        installation_id: GUID identifying the user's installation
        version: Skyline version string (e.g., "25.1.0.237-519d29babc")
        bitness: "64-bit" or "32-bit"
        email: User's email if provided
        stack_trace: The actual stack trace after the separator
    """
    result = {
        'installation_id': None,
        'version': None,
        'bitness': None,
        'email': None,
        'stack_trace': '',
    }

    # Extract Installation ID
    match = INSTALLATION_ID_PATTERN.search(body)
    if match:
        result['installation_id'] = match.group(1)

    # Extract version
    match = VERSION_PATTERN.search(body)
    if match:
        result['version'] = match.group(1)
        result['bitness'] = match.group(2)

    # Extract email if provided (before the stack trace separator)
    header = body.split(STACK_TRACE_SEPARATOR)[0] if STACK_TRACE_SEPARATOR in body else body
    email_match = EMAIL_PATTERN.search(header)
    if email_match:
        result['email'] = email_match.group(0)

    # Extract stack trace (after the separator line)
    if STACK_TRACE_SEPARATOR in body:
        parts = body.split(STACK_TRACE_SEPARATOR, 1)
        if len(parts) > 1:
            result['stack_trace'] = parts[1].strip()

    return result


def _get_history_path():
    """Get path to exception history file in ai/.tmp/history/."""
    history_dir = get_tmp_dir() / 'history'
    history_dir.mkdir(parents=True, exist_ok=True)
    return history_dir / HISTORY_FILE


def _load_exception_history() -> dict:
    """Load existing exception history or create empty structure."""
    import json
    history_path = _get_history_path()

    if history_path.exists():
        try:
            with open(history_path, 'r', encoding='utf-8') as f:
                return json.load(f)
        except (json.JSONDecodeError, IOError) as e:
            logger.warning(f"Could not load exception history: {e}")

    # Return empty structure
    return {
        '_schema_version': 1,
        '_last_updated': None,
        '_retention_months': RETENTION_MONTHS,
        '_release_anchor': MAJOR_RELEASE_VERSION,
        '_release_date': MAJOR_RELEASE_DATE,
        'exceptions': {}
    }


def _save_exception_history(history: dict, report_date: str):
    """Save exception history to file."""
    import json
    history['_last_updated'] = report_date
    history_path = _get_history_path()

    with open(history_path, 'w', encoding='utf-8') as f:
        json.dump(history, f, indent=2, ensure_ascii=False)

    logger.info(f"Saved exception history to {history_path}")


def _age_out_old_entries(history: dict, current_date: str) -> int:
    """Remove entries not seen in RETENTION_MONTHS. Returns count removed."""
    current = datetime.strptime(current_date, "%Y-%m-%d")
    # Approximate months as 30 days each
    cutoff = current - timedelta(days=RETENTION_MONTHS * 30)
    cutoff_str = cutoff.strftime("%Y-%m-%d")

    to_remove = []
    for fp, entry in history.get('exceptions', {}).items():
        last_seen = entry.get('last_seen', '')
        if last_seen and last_seen < cutoff_str:
            to_remove.append(fp)

    for fp in to_remove:
        del history['exceptions'][fp]

    if to_remove:
        logger.info(f"Aged out {len(to_remove)} exceptions not seen since {cutoff_str}")

    return len(to_remove)


def _update_history_with_exceptions(history: dict, parsed_exceptions: list, report_date: str):
    """Merge new exceptions into history.

    Args:
        history: The history dict to update (modified in place)
        parsed_exceptions: List of parsed exception dicts with fingerprint, etc.
        report_date: Current report date YYYY-MM-DD
    """
    exceptions_db = history.setdefault('exceptions', {})

    for exc in parsed_exceptions:
        fp = exc['fingerprint']
        install_id = exc.get('installation_id')
        version = exc.get('version')
        email = exc.get('email')
        sig_frames = exc.get('signature_frames', [])

        if fp not in exceptions_db:
            # New fingerprint - create entry
            exceptions_db[fp] = {
                'fingerprint': fp,
                'signature': ' → '.join(sig_frames) if sig_frames else '(unknown)',
                'exception_type': exc.get('title', '').split('|')[0].strip() if exc.get('title') else None,
                'first_seen': report_date,
                'last_seen': report_date,
                'versions_affected': [],
                'users': {},
                'total_reports': 0,
                'unique_users': 0,
                'emails': [],
                'fix': None,
            }

        entry = exceptions_db[fp]
        entry['last_seen'] = report_date
        entry['total_reports'] = entry.get('total_reports', 0) + 1

        # Track version
        if version and version not in entry.get('versions_affected', []):
            entry.setdefault('versions_affected', []).append(version)

        # Track user
        if install_id:
            users = entry.setdefault('users', {})
            if install_id not in users:
                users[install_id] = {
                    'first_seen': report_date,
                    'last_seen': report_date,
                    'report_count': 0,
                }
            users[install_id]['last_seen'] = report_date
            users[install_id]['report_count'] = users[install_id].get('report_count', 0) + 1
            entry['unique_users'] = len(users)

        # Track email
        if email and email not in entry.get('emails', []):
            entry.setdefault('emails', []).append(email)


def _get_priority_score(entry: dict) -> int:
    """Calculate priority score for an exception entry.

    Higher score = higher priority.
    """
    score = 0

    # More users = higher priority
    unique_users = entry.get('unique_users', 0)
    score += unique_users * 10

    # Has email = actionable
    if entry.get('emails'):
        score += 20

    # More reports = more impact
    total_reports = entry.get('total_reports', 0)
    score += min(total_reports, 10)  # Cap at 10 to not over-weight

    # Fixed but still appearing = critical (regression check done separately)
    if entry.get('fix'):
        # Will be handled in annotation logic
        pass

    return score


def _get_status_annotations(entry: dict, today_reports: int, today_users: int, report_date: str) -> list:
    """Generate status annotations for an exception entry.

    Returns list of annotation strings with emoji.
    """
    annotations = []

    # New today?
    if entry.get('first_seen') == report_date:
        annotations.append("🆕 NEW - First seen today")

    # Has email?
    emails = entry.get('emails', [])
    if emails:
        annotations.append(f"📧 Has user email ({len(emails)} contact(s) for follow-up)")

    # Multi-user?
    unique_users = entry.get('unique_users', 0)
    total_reports = entry.get('total_reports', 0)
    first_seen = entry.get('first_seen', report_date)
    if unique_users > 1 or total_reports > today_reports:
        annotations.append(f"👥 {total_reports} total reports from {unique_users} users since {first_seen}")

    # Known fix?
    fix = entry.get('fix')
    if fix:
        pr = fix.get('pr_number', 'Unknown PR')
        merge_date = fix.get('merge_date', 'Unknown')
        fixed_version = fix.get('fixed_in_version')

        annotations.append(f"✅ KNOWN - Fixed in {pr} (merged {merge_date})")

        # Check for regression - if reports are from versions after fix
        versions_affected = entry.get('versions_affected', [])
        if fixed_version and versions_affected:
            # Simple version comparison (could be improved)
            for v in versions_affected:
                if v > fixed_version:  # String comparison works for semver-ish
                    annotations.append(f"🔴 REGRESSION? Report from {v} (AFTER fix in {fixed_version})")
                    break

    return annotations


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
            # Filter for Parent IS NULL to get only original posts, not responses
            since_date = (datetime.now() - timedelta(days=days)).strftime("%Y-%m-%d")
            filter_array = [
                QueryFilter("Created", since_date, "dategte"),
                QueryFilter("Parent", "", "isblank"),
            ]

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

        Groups exceptions by fingerprint to identify unique bugs.
        Tracks Installation ID to distinguish "1 user hit 4x" from "4 users hit 1x".
        Includes version info for known-fix correlation.

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
            # Filter for Parent IS NULL to get only original posts, not responses
            filter_array = [
                QueryFilter("Created", start_date, "dategte"),
                QueryFilter("Created", end_date, "datelt"),
                QueryFilter("Parent", "", "isblank"),
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

            # Load exception history
            history = _load_exception_history()

            # Parse each exception and compute fingerprints
            parsed_exceptions = []
            for row in rows:
                body = row.get("FormattedBody", "")
                parsed = _parse_exception_body(body)

                # Normalize stack trace and get fingerprint
                norm = normalize_stack_trace(parsed['stack_trace'])

                parsed_exceptions.append({
                    'row_id': row.get("RowId", "?"),
                    'title': row.get("Title", "Unknown"),
                    'created': row.get("Created", "Unknown"),
                    'modified': row.get("Modified", "Unknown"),
                    'status': row.get("Status") or "Unassigned",
                    'assigned_to': row.get("AssignedTo") or "Nobody",
                    'body': body,
                    'installation_id': parsed['installation_id'],
                    'version': parsed['version'],
                    'bitness': parsed['bitness'],
                    'email': parsed['email'],
                    'fingerprint': norm.fingerprint,
                    'signature_frames': norm.signature_frames,
                })

            # Update history with today's exceptions
            _update_history_with_exceptions(history, parsed_exceptions, report_date)

            # Age out old entries
            aged_out = _age_out_old_entries(history, report_date)

            # Group by fingerprint
            fingerprint_groups = {}
            for exc in parsed_exceptions:
                fp = exc['fingerprint']
                if fp not in fingerprint_groups:
                    fingerprint_groups[fp] = []
                fingerprint_groups[fp].append(exc)

            # Build the report
            lines = [
                f"# Exception Report: {report_date}",
                "",
                f"**Total Reports**: {len(rows)}",
                f"**Unique Bugs (by fingerprint)**: {len(fingerprint_groups)}",
                "",
            ]

            # Executive summary - unique bugs
            lines.append("## Executive Summary")
            lines.append("")
            lines.append("| Fingerprint | Reports | Users | Versions | Signature |")
            lines.append("|-------------|---------|-------|----------|-----------|")

            for fp, group in sorted(fingerprint_groups.items(),
                                    key=lambda x: len(x[1]), reverse=True):
                reports = len(group)
                unique_users = len(set(e['installation_id'] for e in group
                                       if e['installation_id']))
                versions = sorted(set(e['version'] for e in group if e['version']))
                versions_str = ', '.join(versions[:3])
                if len(versions) > 3:
                    versions_str += f" (+{len(versions) - 3})"

                # Signature (top frame)
                sig = group[0]['signature_frames']
                sig_str = sig[0] if sig else "(no frames)"

                lines.append(f"| `{fp}` | {reports} | {unique_users} | {versions_str} | {sig_str} |")

            lines.append("")
            lines.append("---")
            lines.append("")

            # Detailed sections by fingerprint
            lines.append("## Exceptions by Bug (Fingerprint)")
            lines.append("")

            # Sort by priority (using history data)
            def get_sort_key(item):
                fp, group = item
                entry = history.get('exceptions', {}).get(fp, {})
                return (-_get_priority_score(entry), -len(group))

            for fp, group in sorted(fingerprint_groups.items(), key=get_sort_key):
                reports = len(group)
                unique_users = len(set(e['installation_id'] for e in group
                                       if e['installation_id']))

                # Get history entry for annotations
                history_entry = history.get('exceptions', {}).get(fp, {})
                annotations = _get_status_annotations(history_entry, reports, unique_users, report_date)

                # Signature frames for this bug
                sig = group[0]['signature_frames']
                sig_str = ' → '.join(sig) if sig else "(no signature frames)"

                lines.append(f"### Bug `{fp}` ({reports} reports, {unique_users} users)")
                lines.append("")

                # Add status annotations
                if annotations:
                    for ann in annotations:
                        lines.append(ann)
                    lines.append("")

                lines.append(f"**Signature**: {sig_str}")
                lines.append("")

                # List versions affected
                versions = sorted(set(e['version'] for e in group if e['version']))
                if versions:
                    lines.append(f"**Versions**: {', '.join(versions)}")
                    lines.append("")

                # Individual reports
                lines.append("**Reports:**")
                lines.append("")

                for exc in group:
                    row_id = exc['row_id']
                    title = exc['title']
                    created = exc['created']
                    install_id = exc['installation_id'] or 'Unknown'
                    version = exc['version'] or 'Unknown'
                    email = exc.get('email')

                    # Format time
                    if isinstance(created, str) and "T" in created:
                        time_str = created.split("T")[1][:8]
                    else:
                        time_str = str(created)

                    lines.append(f"- **#{row_id}** at {time_str}")
                    user_info = f"User: `{install_id[:8]}...`"
                    if email:
                        user_info += f" ({email})"
                    lines.append(f"  - {user_info} | Version: {version}")
                    lines.append(f"  - Title: {title[:60]}{'...' if len(title) > 60 else ''}")
                    lines.append("")

                # Show one full stack trace as reference
                lines.append("<details>")
                lines.append("<summary>Full stack trace (reference)</summary>")
                lines.append("")
                lines.append("```")
                lines.append(group[0]['body'])
                lines.append("```")
                lines.append("</details>")
                lines.append("")
                lines.append("---")
                lines.append("")

            # Save to file
            content = "\n".join(lines)
            date_str = date_obj.strftime("%Y%m%d")
            file_path = get_tmp_dir() / f"exceptions-report-{date_str}.md"
            file_path.write_text(content, encoding="utf-8")

            # Save updated history
            _save_exception_history(history, report_date)
            history_path = _get_history_path()

            # Return summary
            summary_lines = [
                f"Saved exceptions report to {file_path}",
                f"Updated exception history: {history_path}",
                "",
                f"**{report_date}**: {len(rows)} reports → {len(fingerprint_groups)} unique bugs",
                "",
            ]

            # Count history stats
            total_tracked = len(history.get('exceptions', {}))
            if aged_out > 0:
                summary_lines.append(f"📊 History: {total_tracked} bugs tracked ({aged_out} aged out)")
            else:
                summary_lines.append(f"📊 History: {total_tracked} bugs tracked")
            summary_lines.append("")

            # Highlight high-priority items
            high_priority = []
            for fp, group in fingerprint_groups.items():
                entry = history.get('exceptions', {}).get(fp, {})
                annotations = _get_status_annotations(entry, len(group),
                    len(set(e['installation_id'] for e in group if e['installation_id'])),
                    report_date)

                # Flag items needing attention
                if any('NEW' in a for a in annotations):
                    sig = group[0]['signature_frames']
                    sig_str = sig[0] if sig else fp
                    high_priority.append(f"🆕 `{fp}`: {sig_str}")
                elif any('REGRESSION' in a for a in annotations):
                    sig = group[0]['signature_frames']
                    sig_str = sig[0] if sig else fp
                    high_priority.append(f"🔴 `{fp}`: REGRESSION - {sig_str}")
                elif any('email' in a.lower() for a in annotations):
                    sig = group[0]['signature_frames']
                    sig_str = sig[0] if sig else fp
                    high_priority.append(f"📧 `{fp}`: Has contact info - {sig_str}")

            if high_priority:
                summary_lines.append("**Priority items:**")
                for item in high_priority[:5]:  # Limit to top 5
                    summary_lines.append(f"- {item}")

            return "\n".join(summary_lines)

        except Exception as e:
            logger.error(f"Error generating exceptions report: {e}", exc_info=True)
            return f"Error generating exceptions report: {e}"

    @mcp.tool()
    async def record_exception_fix(
        fingerprint: str,
        pr_number: str,
        fixed_in_version: str = None,
        merge_date: str = None,
        commit: str = None,
        notes: str = None,
    ) -> str:
        """Record that an exception fingerprint has been fixed.

        Use this after a PR is merged to prevent future reports from
        flagging this as a new issue.

        Args:
            fingerprint: The 16-char fingerprint from exception reports
            pr_number: PR number (e.g., "PR#1234" or "1234")
            fixed_in_version: Version where fix appears (e.g., "25.1.0.250")
            merge_date: Date PR was merged (YYYY-MM-DD)
            commit: Commit hash of the fix
            notes: Optional notes about the fix
        """
        try:
            history = _load_exception_history()
            exceptions_db = history.get('exceptions', {})

            if fingerprint not in exceptions_db:
                return f"Fingerprint `{fingerprint}` not found in history. Run save_exceptions_report first to populate history."

            entry = exceptions_db[fingerprint]

            # Normalize PR number format
            if pr_number and not pr_number.upper().startswith('PR'):
                pr_number = f"PR#{pr_number}"

            # Record the fix
            entry['fix'] = {
                'pr_number': pr_number,
                'fixed_in_version': fixed_in_version,
                'merge_date': merge_date or datetime.now().strftime("%Y-%m-%d"),
                'commit': commit,
                'notes': notes,
                'recorded_date': datetime.now().strftime("%Y-%m-%d"),
            }

            # Save updated history
            _save_exception_history(history, datetime.now().strftime("%Y-%m-%d"))

            sig = entry.get('signature', fingerprint)
            return (
                f"Recorded fix for `{fingerprint}`:\n"
                f"- Signature: {sig}\n"
                f"- Fixed in: {pr_number}\n"
                f"- Version: {fixed_in_version or 'Not specified'}\n"
                f"- Merge date: {entry['fix']['merge_date']}\n\n"
                f"Future reports will show this as a known fix."
            )

        except Exception as e:
            logger.error(f"Error recording fix: {e}", exc_info=True)
            return f"Error recording fix: {e}"

    @mcp.tool()
    async def query_exception_history(
        top_n: int = 10,
        min_users: int = 1,
        show_fixed: bool = False,
    ) -> str:
        """Query exception history to find high-priority bugs.

        Use this to answer "what should I focus on?" across all tracked exceptions.

        Args:
            top_n: Number of top-priority exceptions to return (default: 10)
            min_users: Minimum unique users to include (default: 1)
            show_fixed: Include exceptions with known fixes (default: False)
        """
        try:
            history = _load_exception_history()
            exceptions_db = history.get('exceptions', {})

            if not exceptions_db:
                return "No exceptions in history. Run save_exceptions_report to populate."

            # Filter and score
            scored = []
            for fp, entry in exceptions_db.items():
                # Skip if below user threshold
                if entry.get('unique_users', 0) < min_users:
                    continue

                # Skip fixed unless requested
                if entry.get('fix') and not show_fixed:
                    continue

                score = _get_priority_score(entry)
                scored.append((score, fp, entry))

            # Sort by score descending
            scored.sort(key=lambda x: -x[0])

            if not scored:
                return f"No exceptions match criteria (min_users={min_users}, show_fixed={show_fixed})"

            lines = [
                f"# Top {min(top_n, len(scored))} Priority Exceptions",
                "",
                f"History contains {len(exceptions_db)} tracked bugs.",
                f"Last updated: {history.get('_last_updated', 'Unknown')}",
                "",
            ]

            for i, (score, fp, entry) in enumerate(scored[:top_n], 1):
                sig = entry.get('signature', '(unknown)')
                users = entry.get('unique_users', 0)
                reports = entry.get('total_reports', 0)
                first_seen = entry.get('first_seen', '?')
                last_seen = entry.get('last_seen', '?')
                emails = entry.get('emails', [])
                fix = entry.get('fix')

                lines.append(f"## {i}. `{fp}` (score: {score})")
                lines.append("")
                lines.append(f"**Signature**: {sig}")
                lines.append(f"**Users**: {users} | **Reports**: {reports}")
                lines.append(f"**First seen**: {first_seen} | **Last seen**: {last_seen}")

                if emails:
                    lines.append(f"📧 **Contact emails**: {', '.join(emails)}")

                if fix:
                    lines.append(f"✅ **Fixed in**: {fix.get('pr_number', '?')} ({fix.get('merge_date', '?')})")

                versions = entry.get('versions_affected', [])
                if versions:
                    lines.append(f"**Versions**: {', '.join(sorted(versions)[:5])}")

                lines.append("")

            return "\n".join(lines)

        except Exception as e:
            logger.error(f"Error querying history: {e}", exc_info=True)
            return f"Error querying history: {e}"

    @mcp.tool()
    async def backfill_exception_history(
        since_date: str = MAJOR_RELEASE_DATE,
        server: str = DEFAULT_SERVER,
        container_path: str = DEFAULT_CONTAINER,
    ) -> str:
        """Backfill exception history from skyline.ms.

        One-time operation to populate history with past exceptions.
        Queries all exceptions since the specified date (default: major release),
        normalizes stack traces, and builds the history database.

        Args:
            since_date: Start date YYYY-MM-DD (default: current major release 2025-05-22)
        """
        try:
            server_context = get_server_context(server, container_path)

            # Query all exceptions since the anchor date
            # Filter for Parent IS NULL to get only original posts, not responses
            filter_array = [
                QueryFilter("Created", since_date, "dategte"),
                QueryFilter("Parent", "", "isblank"),
            ]

            result = labkey.query.select_rows(
                server_context=server_context,
                schema_name=EXCEPTION_SCHEMA,
                query_name=EXCEPTION_QUERY,
                max_rows=10000,  # Should be plenty
                sort="Created",  # Oldest first for proper first_seen tracking
                filter_array=filter_array,
            )

            if not result or not result.get("rows"):
                return f"No exceptions found since {since_date}."

            rows = result["rows"]
            logger.info(f"Backfilling {len(rows)} exceptions since {since_date}")

            # Start fresh history
            history = {
                '_schema_version': 1,
                '_last_updated': None,
                '_retention_months': RETENTION_MONTHS,
                '_release_anchor': MAJOR_RELEASE_VERSION,
                '_release_date': MAJOR_RELEASE_DATE,
                '_backfill_date': datetime.now().strftime("%Y-%m-%d"),
                '_backfill_count': len(rows),
                'exceptions': {}
            }

            exceptions_db = history['exceptions']
            unparseable_rows = []  # Track RowIds we can't parse

            # Process each exception
            for row in rows:
                body = row.get("FormattedBody", "")
                parsed = _parse_exception_body(body)
                created = row.get("Created", "")

                # Extract date from Created timestamp
                if isinstance(created, str) and "T" in created:
                    report_date = created.split("T")[0]
                elif isinstance(created, str) and " " in created:
                    report_date = created.split(" ")[0]
                else:
                    report_date = str(created)[:10]

                # Normalize stack trace and get fingerprint
                norm = normalize_stack_trace(parsed['stack_trace'])
                fp = norm.fingerprint
                sig_frames = norm.signature_frames

                # Track unparseable rows (empty fingerprint = no frames parsed)
                if norm.frame_count == 0:
                    row_id = row.get("RowId")
                    if row_id:
                        unparseable_rows.append(row_id)

                install_id = parsed.get('installation_id')
                version = parsed.get('version')
                email = parsed.get('email')

                if fp not in exceptions_db:
                    # New fingerprint - create entry
                    exceptions_db[fp] = {
                        'fingerprint': fp,
                        'signature': ' → '.join(sig_frames) if sig_frames else '(unknown)',
                        'exception_type': row.get('Title', '').split('|')[0].strip() if row.get('Title') else None,
                        'first_seen': report_date,
                        'last_seen': report_date,
                        'versions_affected': [],
                        'users': {},
                        'total_reports': 0,
                        'unique_users': 0,
                        'emails': [],
                        'fix': None,
                    }

                entry = exceptions_db[fp]

                # Update last_seen (rows are sorted by Created ascending)
                entry['last_seen'] = report_date
                entry['total_reports'] = entry.get('total_reports', 0) + 1

                # Track version
                if version and version not in entry.get('versions_affected', []):
                    entry.setdefault('versions_affected', []).append(version)

                # Track user
                if install_id:
                    users = entry.setdefault('users', {})
                    if install_id not in users:
                        users[install_id] = {
                            'first_seen': report_date,
                            'last_seen': report_date,
                            'report_count': 0,
                        }
                    users[install_id]['last_seen'] = report_date
                    users[install_id]['report_count'] = users[install_id].get('report_count', 0) + 1
                    entry['unique_users'] = len(users)

                # Track email
                if email and email not in entry.get('emails', []):
                    entry.setdefault('emails', []).append(email)

            # Save unparseable RowIds in history for investigation
            if unparseable_rows:
                history['_unparseable_rowids'] = unparseable_rows

            # Save the history
            today = datetime.now().strftime("%Y-%m-%d")
            _save_exception_history(history, today)

            # Generate summary
            total_fingerprints = len(exceptions_db)
            multi_user = sum(1 for e in exceptions_db.values() if e.get('unique_users', 0) > 1)
            with_email = sum(1 for e in exceptions_db.values() if e.get('emails'))

            # Find top issues by report count
            top_issues = sorted(
                exceptions_db.values(),
                key=lambda e: e.get('total_reports', 0),
                reverse=True
            )[:5]

            lines = [
                f"# Exception History Backfill Complete",
                "",
                f"**Source**: {len(rows)} exceptions since {since_date}",
                f"**Unique bugs**: {total_fingerprints} fingerprints",
                f"**Multi-user bugs**: {multi_user}",
                f"**Bugs with contact email**: {with_email}",
                "",
                f"Saved to: {_get_history_path()}",
                "",
                "## Top 5 Most Reported Issues",
                "",
            ]

            for i, entry in enumerate(top_issues, 1):
                fp = entry.get('fingerprint', '?')
                sig = entry.get('signature', '(unknown)')
                reports = entry.get('total_reports', 0)
                users = entry.get('unique_users', 0)
                first = entry.get('first_seen', '?')
                last = entry.get('last_seen', '?')

                lines.append(f"{i}. `{fp}` - {reports} reports, {users} users")
                lines.append(f"   {sig[:60]}{'...' if len(sig) > 60 else ''}")
                lines.append(f"   First: {first} | Last: {last}")
                lines.append("")

            # Add unparseable RowIds section if any
            if unparseable_rows:
                lines.append("## Unparseable Exceptions")
                lines.append(f"")
                lines.append(f"{len(unparseable_rows)} rows could not be parsed. RowIds:")
                lines.append(f"")
                # Show up to 20, or all if fewer
                display_rows = unparseable_rows[:20]
                lines.append(", ".join(str(r) for r in display_rows))
                if len(unparseable_rows) > 20:
                    lines.append(f"... and {len(unparseable_rows) - 20} more (see _unparseable_rowids in history file)")
                lines.append("")

            return "\n".join(lines)

        except Exception as e:
            logger.error(f"Error backfilling history: {e}", exc_info=True)
            return f"Error backfilling history: {e}"
