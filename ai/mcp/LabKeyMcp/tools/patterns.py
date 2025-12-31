"""Pattern detection tools for daily report analysis.

This module provides tools for detecting patterns across daily test results,
comparing today's data against historical data to identify:
- NEW failures (not in previous day)
- RESOLVED failures (fixed since yesterday)
- SYSTEMIC issues (affecting all machines)
- EXTERNAL service issues (Koina, Panorama, etc.)
- RECURRING missing computers
"""

import json
import logging
from datetime import datetime, timedelta
from pathlib import Path
from typing import Optional

from .common import get_tmp_dir

logger = logging.getLogger("labkey_mcp")

# Known external services that tests depend on
EXTERNAL_SERVICES = {
    "koina": ["Koina", "koina", "KOINA"],
    "panorama": ["Panorama", "panorama", "PanoramaServer", "PanoramaClient"],
    "uniprot": ["UniProt", "uniprot", "UNIPROT"],
    "prosit": ["Prosit", "prosit", "PROSIT"],
}


def _get_history_dir() -> Path:
    """Get the history directory for daily summaries."""
    history_dir = get_tmp_dir() / "history"
    history_dir.mkdir(exist_ok=True)
    return history_dir


def _load_daily_summary(date_str: str) -> Optional[dict]:
    """Load a daily summary JSON file for a given date.

    Args:
        date_str: Date in YYYY-MM-DD format

    Returns:
        Parsed JSON dict, or None if file doesn't exist
    """
    history_dir = _get_history_dir()
    date_compact = date_str.replace("-", "")
    file_path = history_dir / f"daily-summary-{date_compact}.json"

    if not file_path.exists():
        logger.info(f"No historical data for {date_str}: {file_path}")
        return None

    try:
        with open(file_path, "r", encoding="utf-8") as f:
            return json.load(f)
    except (json.JSONDecodeError, IOError) as e:
        logger.error(f"Error loading {file_path}: {e}")
        return None


def _extract_test_names(data: dict, category: str) -> set:
    """Extract test names from a daily summary category.

    Args:
        data: Daily summary dict
        category: One of 'failures', 'leaks', 'hangs'

    Returns:
        Set of test names
    """
    if not data or "nightly" not in data:
        return set()

    nightly = data["nightly"]
    if category not in nightly:
        return set()

    # The structure is {"TestName": ["COMPUTER1", "COMPUTER2"]}
    return set(nightly[category].keys())


def _extract_test_computers(data: dict, category: str) -> dict:
    """Extract test-to-computers mapping from a daily summary.

    Args:
        data: Daily summary dict
        category: One of 'failures', 'leaks', 'hangs'

    Returns:
        Dict of {test_name: [computers]}
    """
    if not data or "nightly" not in data:
        return {}

    nightly = data["nightly"]
    if category not in nightly:
        return {}

    return nightly[category]


def _detect_external_service(test_name: str) -> Optional[str]:
    """Check if a test name suggests external service dependency.

    Args:
        test_name: Name of the test

    Returns:
        Service name if detected, None otherwise
    """
    for service, patterns in EXTERNAL_SERVICES.items():
        for pattern in patterns:
            if pattern in test_name:
                return service
    return None


def _get_expected_computers(data: dict) -> set:
    """Get the set of computers that reported in a daily summary.

    This is inferred from the failures/leaks data since we don't store
    the full computer list in the summary.
    """
    computers = set()
    if not data or "nightly" not in data:
        return computers

    nightly = data["nightly"]

    # Collect computers from failures, leaks, and hangs
    for category in ["failures", "leaks", "hangs"]:
        if category in nightly:
            for test_name, comps in nightly[category].items():
                computers.update(comps)

    # Also include missing computers
    if "missing_computers" in nightly:
        computers.update(nightly["missing_computers"])

    return computers


def register_tools(mcp):
    """Register pattern detection tools."""

    @mcp.tool()
    async def analyze_daily_patterns(
        report_date: str,
        days_back: int = 7,
    ) -> str:
        """**PRIMARY**: Analyze patterns in daily test results compared to history.

        Compares today's results against historical data to identify:
        - NEW: Failures/leaks not seen yesterday
        - RESOLVED: Issues fixed since yesterday
        - SYSTEMIC: Issues affecting all machines (high priority)
        - EXTERNAL: Issues involving external services (Koina, Panorama)
        - RECURRING: Computers missing for multiple consecutive days

        Args:
            report_date: Date to analyze in YYYY-MM-DD format
            days_back: Number of days of history to consider (default: 7)

        Returns:
            Structured pattern analysis with prioritized action items.
        """
        today = _load_daily_summary(report_date)

        if not today:
            return (
                f"No daily summary found for {report_date}.\n\n"
                f"Pattern detection requires historical JSON data.\n"
                f"Run /pw-daily first to generate ai/.tmp/history/daily-summary-{report_date.replace('-', '')}.json"
            )

        # Load yesterday's data
        yesterday_date = (datetime.strptime(report_date, "%Y-%m-%d") - timedelta(days=1)).strftime("%Y-%m-%d")
        yesterday = _load_daily_summary(yesterday_date)

        # Load historical data for trend analysis
        history = []
        for i in range(2, days_back + 1):
            hist_date = (datetime.strptime(report_date, "%Y-%m-%d") - timedelta(days=i)).strftime("%Y-%m-%d")
            hist_data = _load_daily_summary(hist_date)
            if hist_data:
                history.append((hist_date, hist_data))

        # Extract today's issues
        today_failures = _extract_test_names(today, "failures")
        today_leaks = _extract_test_names(today, "leaks")
        today_hangs = _extract_test_names(today, "hangs")
        today_missing = set(today.get("nightly", {}).get("missing_computers", []))

        # Extract yesterday's issues (if available)
        if yesterday:
            yesterday_failures = _extract_test_names(yesterday, "failures")
            yesterday_leaks = _extract_test_names(yesterday, "leaks")
            yesterday_hangs = _extract_test_names(yesterday, "hangs")
            yesterday_missing = set(yesterday.get("nightly", {}).get("missing_computers", []))
        else:
            yesterday_failures = set()
            yesterday_leaks = set()
            yesterday_hangs = set()
            yesterday_missing = set()

        # Compute patterns
        new_failures = today_failures - yesterday_failures
        resolved_failures = yesterday_failures - today_failures
        new_leaks = today_leaks - yesterday_leaks
        resolved_leaks = yesterday_leaks - today_leaks
        new_hangs = today_hangs - yesterday_hangs

        # Detect systemic issues (all machines affected)
        # Get computer counts for failures
        failure_computers = _extract_test_computers(today, "failures")
        leak_computers = _extract_test_computers(today, "leaks")

        # Consider "systemic" if a test fails on 3+ machines
        systemic_failures = {
            test: comps for test, comps in failure_computers.items()
            if len(comps) >= 3
        }
        systemic_leaks = {
            test: comps for test, comps in leak_computers.items()
            if len(comps) >= 3
        }

        # Detect external service issues
        external_failures = {}
        for test in today_failures:
            service = _detect_external_service(test)
            if service:
                if service not in external_failures:
                    external_failures[service] = []
                external_failures[service].append(test)

        # Track recurring missing computers
        recurring_missing = {}
        for computer in today_missing:
            days_missing = 1
            if computer in yesterday_missing:
                days_missing += 1
                for hist_date, hist_data in history:
                    hist_missing = set(hist_data.get("nightly", {}).get("missing_computers", []))
                    if computer in hist_missing:
                        days_missing += 1
                    else:
                        break
            if days_missing >= 2:
                recurring_missing[computer] = days_missing

        # Build output
        lines = [
            f"# Pattern Analysis for {report_date}",
            "",
            f"Comparing against: {yesterday_date}" + (" (no data)" if not yesterday else ""),
            f"Historical data: {len(history)} additional days loaded",
            "",
        ]

        # Priority Action Items section
        action_items = []

        # Systemic issues are highest priority
        for test, comps in systemic_failures.items():
            action_items.append(f"🔴 SYSTEMIC FAILURE: {test} (failing on {len(comps)} machines: {', '.join(sorted(comps))})")

        for test, comps in systemic_leaks.items():
            action_items.append(f"🔴 SYSTEMIC LEAK: {test} (leaking on {len(comps)} machines: {', '.join(sorted(comps))})")

        # New failures (regressions)
        for test in sorted(new_failures):
            if test not in systemic_failures:  # Don't duplicate
                service = _detect_external_service(test)
                if service:
                    action_items.append(f"🆕 NEW + EXTERNAL ({service}): {test}")
                else:
                    action_items.append(f"🆕 NEW FAILURE: {test}")

        # New leaks
        for test in sorted(new_leaks):
            if test not in systemic_leaks:
                action_items.append(f"🆕 NEW LEAK: {test}")

        # New hangs
        for test in sorted(new_hangs):
            action_items.append(f"🆕 NEW HANG: {test}")

        # External service issues (not already flagged as NEW)
        for service, tests in external_failures.items():
            existing_tests = [t for t in tests if t not in new_failures]
            if existing_tests:
                action_items.append(f"🌐 EXTERNAL ({service}): {', '.join(sorted(existing_tests))}")

        # Recurring missing computers
        for computer, days in sorted(recurring_missing.items(), key=lambda x: -x[1]):
            action_items.append(f"⚠️ MISSING {days} DAYS: {computer}")

        if action_items:
            lines.append("## 🎯 Action Items (Prioritized)")
            lines.append("")
            for item in action_items:
                lines.append(f"- {item}")
            lines.append("")
        else:
            lines.append("## ✅ No Action Items")
            lines.append("")
            lines.append("No new issues or patterns requiring attention.")
            lines.append("")

        # Resolved section (good news)
        if resolved_failures or resolved_leaks:
            lines.append("## ✅ Resolved Since Yesterday")
            lines.append("")
            for test in sorted(resolved_failures):
                lines.append(f"- ✅ {test} (failure resolved)")
            for test in sorted(resolved_leaks):
                lines.append(f"- ✅ {test} (leak resolved)")
            lines.append("")

        # Summary statistics
        lines.append("## Summary Statistics")
        lines.append("")
        lines.append(f"| Category | Today | Yesterday | New | Resolved |")
        lines.append(f"|----------|-------|-----------|-----|----------|")
        lines.append(f"| Failures | {len(today_failures)} | {len(yesterday_failures)} | {len(new_failures)} | {len(resolved_failures)} |")
        lines.append(f"| Leaks | {len(today_leaks)} | {len(yesterday_leaks)} | {len(new_leaks)} | {len(resolved_leaks)} |")
        lines.append(f"| Hangs | {len(today_hangs)} | {len(yesterday_hangs)} | {len(new_hangs)} | - |")
        lines.append(f"| Missing | {len(today_missing)} | {len(yesterday_missing)} | - | - |")
        lines.append("")

        # Pattern explanations
        lines.append("## Pattern Legend")
        lines.append("")
        lines.append("- 🔴 **SYSTEMIC**: Affects 3+ machines - likely code issue, not environment")
        lines.append("- 🆕 **NEW**: First appearance since yesterday - likely regression")
        lines.append("- 🌐 **EXTERNAL**: Test involves external service (may be service issue)")
        lines.append("- ⚠️ **MISSING N DAYS**: Computer hasn't reported for multiple days")
        lines.append("- ✅ **RESOLVED**: Issue no longer present (fixed or intermittent)")
        lines.append("")

        return "\n".join(lines)

    @mcp.tool()
    async def save_daily_summary(
        report_date: str,
        nightly_summary: str,
        nightly_failures: str,
        nightly_leaks: str,
        nightly_hangs: str,
        missing_computers: str,
        exception_count: int = 0,
        exception_signatures: str = "{}",
        support_threads: int = 0,
    ) -> str:
        """Save a daily summary JSON for pattern detection.

        This tool creates the historical JSON that analyze_daily_patterns uses.
        Call this at the end of each /pw-daily run to build history.

        Args:
            report_date: Date in YYYY-MM-DD format
            nightly_summary: JSON string like {"errors": N, "warnings": N, "passed": N, "missing": N, "total_tests": N}
            nightly_failures: JSON string like {"TestName": ["COMPUTER1", "COMPUTER2"]}
            nightly_leaks: JSON string like {"TestName": ["COMPUTER1"]}
            nightly_hangs: JSON string like {"TestName": ["COMPUTER1"]}
            missing_computers: JSON string like ["COMPUTER1", "COMPUTER2"]
            exception_count: Number of exceptions
            exception_signatures: JSON string of exception signatures
            support_threads: Number of support threads needing attention

        Returns:
            Confirmation with file path.
        """
        try:
            # Parse JSON inputs
            summary = json.loads(nightly_summary) if nightly_summary else {}
            failures = json.loads(nightly_failures) if nightly_failures else {}
            leaks = json.loads(nightly_leaks) if nightly_leaks else {}
            hangs = json.loads(nightly_hangs) if nightly_hangs else {}
            missing = json.loads(missing_computers) if missing_computers else []
            exc_sigs = json.loads(exception_signatures) if exception_signatures else {}

            # Build the daily summary structure
            daily_summary = {
                "date": report_date,
                "generated_at": datetime.now().isoformat(),
                "nightly": {
                    "summary": summary,
                    "failures": failures,
                    "leaks": leaks,
                    "hangs": hangs,
                    "missing_computers": missing,
                },
                "exceptions": {
                    "count": exception_count,
                    "by_signature": exc_sigs,
                },
                "support": {
                    "threads_needing_attention": support_threads,
                },
            }

            # Save to file
            history_dir = _get_history_dir()
            date_compact = report_date.replace("-", "")
            file_path = history_dir / f"daily-summary-{date_compact}.json"

            with open(file_path, "w", encoding="utf-8") as f:
                json.dump(daily_summary, f, indent=2)

            return (
                f"Daily summary saved successfully:\n"
                f"  file_path: {file_path}\n"
                f"  date: {report_date}\n"
                f"  failures: {len(failures)} tests\n"
                f"  leaks: {len(leaks)} tests\n"
                f"  hangs: {len(hangs)} tests\n"
                f"  missing: {len(missing)} computers\n"
                f"  exceptions: {exception_count}\n"
                f"\nUse analyze_daily_patterns('{report_date}') to compare with history."
            )

        except json.JSONDecodeError as e:
            return f"Error parsing JSON input: {e}"
        except Exception as e:
            logger.error(f"Error saving daily summary: {e}", exc_info=True)
            return f"Error saving daily summary: {e}"
