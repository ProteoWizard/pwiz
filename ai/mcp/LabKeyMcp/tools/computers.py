"""Computer status tools for LabKey MCP server.

This module provides tools for managing nightly test computer status:
- Deactivate computers (they won't appear as "missing" in daily reports)
- Reactivate computers when ready to return to nightly testing
- Track alarm dates to remind about reactivation
- List computer status across test folders

The "active" flag in LabKey's userdata table controls whether a computer
is expected to report. When active=false, the computer won't appear in
the daily "missing computers" warnings.

Use cases:
- BRENDANX-UW6: Temporarily out for tutorial screenshot work
- DSHTEYN-DEV01: Out for debugging/development work
- Hardware maintenance, OS upgrades, etc.
"""

import json
import logging
from datetime import datetime, date
from pathlib import Path
from typing import Optional
from urllib.parse import quote, urlencode

import labkey

from .common import (
    get_server_context,
    get_tmp_dir,
    DEFAULT_SERVER,
    TESTRESULTS_SCHEMA,
    DEFAULT_TEST_CONTAINER,
)
from .wiki import _get_labkey_session

logger = logging.getLogger("labkey_mcp")


# All test folders that track computers
TEST_FOLDERS = [
    "/home/development/Nightly x64",
    "/home/development/Nightly Zip x64",
    "/home/development/Nightly x86",
    "/home/development/Performance",
    "/home/development/Code Inspection",
    "/home/development/Stress",
]


def _get_history_file() -> Path:
    """Get path to the computer status history file."""
    history_dir = get_tmp_dir() / "history"
    history_dir.mkdir(exist_ok=True)
    return history_dir / "computer-status.json"


def _load_status_history() -> dict:
    """Load computer status history from JSON file.

    Returns:
        Dict with structure:
        {
            "deactivations": {
                "COMPUTER_NAME": {
                    "reason": "Tutorial screenshot work",
                    "deactivated_date": "2026-01-02",
                    "alarm_date": "2026-01-09",  # Optional
                    "alarm_note": "Check if screenshots done",  # Optional
                    "folders": ["/home/development/Nightly x64", ...]
                }
            }
        }
    """
    history_file = _get_history_file()
    if history_file.exists():
        try:
            return json.loads(history_file.read_text(encoding="utf-8"))
        except Exception as e:
            logger.warning(f"Error loading status history: {e}")
    return {"deactivations": {}}


def _save_status_history(history: dict):
    """Save computer status history to JSON file."""
    history_file = _get_history_file()
    history_file.write_text(
        json.dumps(history, indent=2, default=str),
        encoding="utf-8"
    )


def _get_user_id(
    computer_name: str,
    server: str,
    container_path: str,
) -> Optional[int]:
    """Query LabKey to get userId for a computer name.

    Args:
        computer_name: Computer name (e.g., "BRENDANX-UW6")
        server: LabKey server hostname
        container_path: Container path (test folder)

    Returns:
        userId (int) or None if not found
    """
    server_context = get_server_context(server, container_path)

    result = labkey.query.select_rows(
        server_context=server_context,
        schema_name=TESTRESULTS_SCHEMA,
        query_name="user",
        filter_array=[
            labkey.query.QueryFilter("username", computer_name, "eq"),
        ],
        max_rows=1,
    )

    if result and result.get("rows"):
        return result["rows"][0].get("id")
    return None


def _set_computer_active(
    user_id: int,
    active: bool,
    server: str,
    container_path: str,
) -> tuple[bool, str]:
    """Set the active status for a computer in LabKey.

    Args:
        user_id: The user.id value from the user table
        active: True to activate, False to deactivate
        server: LabKey server hostname
        container_path: Container path (test folder)

    Returns:
        Tuple of (success: bool, message: str)
    """
    try:
        # Get authenticated session with CSRF token
        session, csrf = _get_labkey_session(server)

        # Build URL for setUserActive endpoint
        encoded_path = quote(container_path, safe="/")
        params = urlencode({"active": str(active).lower(), "userId": user_id})
        url = f"https://{server}{encoded_path}/testresults-setUserActive.view?{params}"

        # POST request (empty body, params in URL)
        logger.info(f"Setting computer active={active} for userId={user_id} in {container_path}")
        status_code, result = session.post_json(url, {})

        if status_code == 200:
            return True, f"Successfully set active={active}"
        else:
            error = result.get("error", str(result)[:200])
            return False, f"HTTP {status_code}: {error}"

    except Exception as e:
        logger.error(f"Error setting computer active status: {e}", exc_info=True)
        return False, str(e)


def register_tools(mcp):
    """Register computer status tools."""

    @mcp.tool()
    async def deactivate_computer(
        computer_name: str,
        reason: str,
        alarm_date: Optional[str] = None,
        alarm_note: Optional[str] = None,
        container_path: str = DEFAULT_TEST_CONTAINER,
        server: str = DEFAULT_SERVER,
    ) -> str:
        """Deactivate a computer from nightly test expectations.

        When deactivated, the computer won't appear as "missing" in daily reports.
        Optionally set an alarm date to remind about reactivation.

        Args:
            computer_name: Computer name (e.g., "BRENDANX-UW6")
            reason: Why the computer is being deactivated (e.g., "Tutorial screenshot work")
            alarm_date: Optional date (YYYY-MM-DD) to remind about reactivation
            alarm_note: Optional note to show when alarm is due
            container_path: Test folder (default: /home/development/Nightly x64)
        """
        try:
            # Step 1: Get userId from computer name
            user_id = _get_user_id(computer_name, server, container_path)
            if not user_id:
                return (
                    f"Computer '{computer_name}' not found in {container_path}.\n"
                    f"Check the computer name and container path."
                )

            # Step 2: Call setUserActive to deactivate
            success, message = _set_computer_active(
                user_id, active=False, server=server, container_path=container_path
            )

            if not success:
                return f"Failed to deactivate {computer_name}: {message}"

            # Step 3: Record in local history with alarm
            history = _load_status_history()
            today = date.today().isoformat()

            if computer_name not in history["deactivations"]:
                history["deactivations"][computer_name] = {
                    "reason": reason,
                    "deactivated_date": today,
                    "folders": [container_path],
                }
            else:
                # Update existing entry
                entry = history["deactivations"][computer_name]
                entry["reason"] = reason
                if container_path not in entry.get("folders", []):
                    entry.setdefault("folders", []).append(container_path)

            entry = history["deactivations"][computer_name]
            if alarm_date:
                entry["alarm_date"] = alarm_date
            if alarm_note:
                entry["alarm_note"] = alarm_note

            _save_status_history(history)

            # Build response
            lines = [
                f"Computer deactivated successfully:",
                f"  computer: {computer_name}",
                f"  user_id: {user_id}",
                f"  folder: {container_path}",
                f"  reason: {reason}",
                f"  date: {today}",
            ]
            if alarm_date:
                lines.append(f"  alarm_date: {alarm_date}")
            if alarm_note:
                lines.append(f"  alarm_note: {alarm_note}")

            lines.extend([
                "",
                "The computer will no longer appear as 'missing' in daily reports.",
                "Use reactivate_computer() when ready to resume nightly testing.",
            ])

            return "\n".join(lines)

        except Exception as e:
            logger.error(f"Error deactivating computer: {e}", exc_info=True)
            return f"Error deactivating computer: {e}"

    @mcp.tool()
    async def reactivate_computer(
        computer_name: str,
        container_path: str = DEFAULT_TEST_CONTAINER,
        server: str = DEFAULT_SERVER,
    ) -> str:
        """Reactivate a computer for nightly test expectations.

        The computer will again appear in "missing" warnings if it doesn't report.
        Clears any alarm set for this computer.

        Args:
            computer_name: Computer name (e.g., "BRENDANX-UW6")
            container_path: Test folder (default: /home/development/Nightly x64)
        """
        try:
            # Step 1: Get userId from computer name
            user_id = _get_user_id(computer_name, server, container_path)
            if not user_id:
                return (
                    f"Computer '{computer_name}' not found in {container_path}.\n"
                    f"Check the computer name and container path."
                )

            # Step 2: Call setUserActive to reactivate
            success, message = _set_computer_active(
                user_id, active=True, server=server, container_path=container_path
            )

            if not success:
                return f"Failed to reactivate {computer_name}: {message}"

            # Step 3: Update local history
            history = _load_status_history()

            if computer_name in history["deactivations"]:
                entry = history["deactivations"][computer_name]
                # Remove this folder from the list
                if container_path in entry.get("folders", []):
                    entry["folders"].remove(container_path)
                # If no folders left, remove the entire entry
                if not entry.get("folders"):
                    del history["deactivations"][computer_name]

            _save_status_history(history)

            return (
                f"Computer reactivated successfully:\n"
                f"  computer: {computer_name}\n"
                f"  user_id: {user_id}\n"
                f"  folder: {container_path}\n"
                f"\n"
                f"The computer will now appear in 'missing' warnings if it doesn't report."
            )

        except Exception as e:
            logger.error(f"Error reactivating computer: {e}", exc_info=True)
            return f"Error reactivating computer: {e}"

    @mcp.tool()
    async def list_computer_status(
        container_path: str = DEFAULT_TEST_CONTAINER,
        server: str = DEFAULT_SERVER,
    ) -> str:
        """List all computers and their active status in a test folder.

        Shows which computers are active (expected to report) and which
        are deactivated (won't trigger "missing" warnings).

        Args:
            container_path: Test folder (default: /home/development/Nightly x64)
        """
        try:
            server_context = get_server_context(server, container_path)

            # Query all_computers which joins user and userdata with LEFT OUTER JOIN
            result = labkey.query.select_rows(
                server_context=server_context,
                schema_name=TESTRESULTS_SCHEMA,
                query_name="all_computers",
                max_rows=100,
            )

            if not result or not result.get("rows"):
                return f"No computers found in {container_path}"

            # Load local history for alarm info
            history = _load_status_history()
            today = date.today()

            active_computers = []
            inactive_computers = []

            for row in result["rows"]:
                name = row.get("computer", "?")
                is_active = row.get("active", True)

                # Get local alarm info if available
                local_info = history.get("deactivations", {}).get(name, {})

                if is_active:
                    active_computers.append(name)
                else:
                    info = {"name": name}
                    if local_info:
                        info["reason"] = local_info.get("reason", "Unknown")
                        info["since"] = local_info.get("deactivated_date", "Unknown")
                        if local_info.get("alarm_date"):
                            alarm = date.fromisoformat(local_info["alarm_date"])
                            if alarm <= today:
                                info["alarm"] = f"OVERDUE ({local_info['alarm_date']})"
                            else:
                                info["alarm"] = local_info["alarm_date"]
                            if local_info.get("alarm_note"):
                                info["alarm_note"] = local_info["alarm_note"]
                    inactive_computers.append(info)

            # Build output
            lines = [
                f"Computer Status for {container_path}",
                "=" * 50,
                "",
                f"ACTIVE ({len(active_computers)} computers):",
            ]

            for name in active_computers:
                lines.append(f"  - {name}")

            lines.extend([
                "",
                f"INACTIVE ({len(inactive_computers)} computers):",
            ])

            if inactive_computers:
                for info in inactive_computers:
                    line = f"  - {info['name']}"
                    if info.get("reason"):
                        line += f" - {info['reason']}"
                    if info.get("since"):
                        line += f" (since {info['since']})"
                    lines.append(line)
                    if info.get("alarm"):
                        alarm_line = f"      Alarm: {info['alarm']}"
                        if info.get("alarm_note"):
                            alarm_line += f" - {info['alarm_note']}"
                        lines.append(alarm_line)
            else:
                lines.append("  (none)")

            return "\n".join(lines)

        except Exception as e:
            logger.error(f"Error listing computer status: {e}", exc_info=True)
            return f"Error listing computer status: {e}"

    @mcp.tool()
    async def check_computer_alarms() -> str:
        """Check for computer reactivation alarms that are due.

        Returns any alarms with dates on or before today.
        Use this during daily report generation to remind about computers
        that should be reactivated.
        """
        try:
            history = _load_status_history()
            today = date.today()

            due_alarms = []
            upcoming_alarms = []

            for computer_name, info in history.get("deactivations", {}).items():
                if info.get("alarm_date"):
                    alarm_date = date.fromisoformat(info["alarm_date"])
                    entry = {
                        "computer": computer_name,
                        "reason": info.get("reason", "Unknown"),
                        "alarm_date": info["alarm_date"],
                        "alarm_note": info.get("alarm_note"),
                        "folders": info.get("folders", []),
                        "deactivated_date": info.get("deactivated_date"),
                    }

                    if alarm_date <= today:
                        days_overdue = (today - alarm_date).days
                        entry["days_overdue"] = days_overdue
                        due_alarms.append(entry)
                    else:
                        days_until = (alarm_date - today).days
                        entry["days_until"] = days_until
                        if days_until <= 7:  # Show upcoming within a week
                            upcoming_alarms.append(entry)

            if not due_alarms and not upcoming_alarms:
                return "No computer alarms due or upcoming within 7 days."

            lines = []

            if due_alarms:
                lines.append("DUE OR OVERDUE ALARMS:")
                lines.append("-" * 40)
                for alarm in sorted(due_alarms, key=lambda x: x["days_overdue"], reverse=True):
                    if alarm["days_overdue"] == 0:
                        status = "DUE TODAY"
                    else:
                        status = f"OVERDUE {alarm['days_overdue']} days"

                    lines.append(f"  {alarm['computer']} - {status}")
                    lines.append(f"    Reason: {alarm['reason']}")
                    if alarm.get("alarm_note"):
                        lines.append(f"    Note: {alarm['alarm_note']}")
                    lines.append(f"    Folders: {', '.join(alarm.get('folders', ['?']))}")
                    lines.append("")

            if upcoming_alarms:
                lines.append("UPCOMING ALARMS (next 7 days):")
                lines.append("-" * 40)
                for alarm in sorted(upcoming_alarms, key=lambda x: x["days_until"]):
                    lines.append(f"  {alarm['computer']} - in {alarm['days_until']} days ({alarm['alarm_date']})")
                    lines.append(f"    Reason: {alarm['reason']}")
                    lines.append("")

            return "\n".join(lines)

        except Exception as e:
            logger.error(f"Error checking computer alarms: {e}", exc_info=True)
            return f"Error checking computer alarms: {e}"
