"""Nightly test analysis tools for LabKey MCP server.

This module contains tools for querying and analyzing Skyline nightly test results
from the skyline.ms test infrastructure.
"""

import json
import logging
from collections import defaultdict
from datetime import datetime, timedelta
from typing import Optional
from urllib.parse import quote

import labkey
from labkey.query import QueryFilter

from .common import (
    get_server_context,
    get_netrc_credentials,
    get_tmp_dir,
    make_authenticated_request,
    DEFAULT_SERVER,
    DEFAULT_TEST_CONTAINER,
    TESTRESULTS_SCHEMA,
)
from .stacktrace import normalize_stack_trace, group_by_fingerprint

logger = logging.getLogger("labkey_mcp")


def register_tools(mcp):
    """Register nightly test analysis tools."""

    @mcp.tool()
    async def query_test_runs(
        days: int = 7,
        max_rows: int = 20,
        server: str = DEFAULT_SERVER,
        container_path: str = DEFAULT_TEST_CONTAINER,
    ) -> str:
        """**DRILL-DOWN**: Browse test runs in a folder. Prefer get_daily_test_summary for daily review.

        Args:
            days: Days back to query (default: 7)
            container_path: Test folder (default: Nightly x64)
        """
        try:
            server_context = get_server_context(server, container_path)

            since_date = (datetime.now() - timedelta(days=days)).strftime("%Y-%m-%d")
            filter_array = [QueryFilter("posttime", since_date, "dategte")]

            result = labkey.query.select_rows(
                server_context=server_context,
                schema_name=TESTRESULTS_SCHEMA,
                query_name="testruns",
                max_rows=max_rows,
                sort="-posttime",
                filter_array=filter_array,
            )

            if result and result.get("rows"):
                rows = result["rows"]
                total = result.get("rowCount", len(rows))
                lines = [
                    f"Found {total} test runs in the last {days} days (showing {len(rows)}):",
                    "",
                ]
                for row in rows:
                    run_id = row.get("id", "?")
                    posttime = row.get("posttime", "Unknown")
                    passed = row.get("passedtests", 0)
                    failed = row.get("failedtests", 0)
                    leaked = row.get("leakedtests", 0)
                    duration = row.get("duration", 0)
                    avg_mem = row.get("averagemem", 0)
                    revision = row.get("revision", "?")
                    flagged = row.get("flagged", False)

                    status = "FLAGGED " if flagged else ""
                    lines.append(f"--- Run #{run_id} ({posttime}) {status}---")
                    lines.append(f"  Tests: {passed} passed, {failed} failed, {leaked} leaked")
                    lines.append(f"  Duration: {duration} min, Avg Memory: {avg_mem} MB")
                    lines.append(f"  Revision: {revision}")
                    lines.append("")

                return "\n".join(lines)
            return f"No test runs found in the last {days} days."

        except Exception as e:
            logger.error(f"Error querying test runs: {e}", exc_info=True)
            return f"Error querying test runs: {e}"

    @mcp.tool()
    async def get_run_failures(
        run_id: int,
        server: str = DEFAULT_SERVER,
        container_path: str = DEFAULT_TEST_CONTAINER,
    ) -> str:
        """**DRILL-DOWN**: Get stack traces for failed tests in a specific run.

        Args:
            run_id: Test run ID from get_daily_test_summary
            container_path: Test folder (must match the run's folder)
        """
        try:
            server_context = get_server_context(server, container_path)
            filter_array = [QueryFilter("testrunid", str(run_id), "eq")]

            result = labkey.query.select_rows(
                server_context=server_context,
                schema_name=TESTRESULTS_SCHEMA,
                query_name="testfails",
                max_rows=100,
                filter_array=filter_array,
            )

            if result and result.get("rows"):
                rows = result["rows"]
                lines = [f"Failed tests for run #{run_id} ({len(rows)} failures):", ""]

                for row in rows:
                    testname = row.get("testname", "Unknown")
                    stacktrace = row.get("stacktrace", "")
                    pass_num = row.get("pass", "?")

                    lines.append(f"=== {testname} (pass {pass_num}) ===")
                    if len(stacktrace) > 1000:
                        stacktrace = stacktrace[:1000] + "\n... (truncated)"
                    lines.append(stacktrace)
                    lines.append("")

                return "\n".join(lines)
            return f"No failures found for run #{run_id}"

        except Exception as e:
            logger.error(f"Error getting run failures: {e}", exc_info=True)
            return f"Error getting run failures: {e}"

    @mcp.tool()
    async def save_run_log(
        run_id: int,
        server: str = DEFAULT_SERVER,
        container_path: str = DEFAULT_TEST_CONTAINER,
    ) -> str:
        """**DRILL-DOWN**: Full test run log (9-12 hours output). Saves to ai/.tmp/testrun-log-{run_id}.txt.

        Args:
            run_id: Test run ID
            container_path: Test folder (must match the run's folder)
        """
        try:
            # URL-encode the container path for the URL
            encoded_path = quote(container_path, safe='/')
            log_url = f"https://{server}{encoded_path}/testresults-viewLog.view?runid={run_id}"

            logger.info(f"Fetching log from: {log_url}")

            # Make authenticated HTTP request
            response_bytes = make_authenticated_request(server, log_url, timeout=120)
            response_text = response_bytes.decode("utf-8")

            # Parse JSON response - endpoint returns {log: "..."}
            data = json.loads(response_text)
            log_content = data.get("log", "")

            if not log_content:
                return f"Test run #{run_id} has no log content"

            # Save to tmp directory
            output_dir = get_tmp_dir()
            output_file = output_dir / f"testrun-log-{run_id}.txt"
            output_file.write_text(log_content, encoding="utf-8")

            # Calculate metadata
            size_bytes = output_file.stat().st_size
            line_count = log_content.count("\n") + 1

            return (
                f"Log saved successfully:\n"
                f"  file_path: {output_file}\n"
                f"  size_bytes: {size_bytes:,}\n"
                f"  line_count: {line_count:,}\n"
                f"\nUse Grep or Read tools to search within this file."
            )

        except Exception as e:
            logger.error(f"Error saving run log: {e}", exc_info=True)
            return f"Error saving run log: {e}"

    @mcp.tool()
    async def save_run_xml(
        run_id: int,
        server: str = DEFAULT_SERVER,
        container_path: str = DEFAULT_TEST_CONTAINER,
    ) -> str:
        """**DRILL-DOWN**: Structured test results XML. Saves to ai/.tmp/testrun-xml-{run_id}.xml.

        Args:
            run_id: Test run ID
            container_path: Test folder (must match the run's folder)
        """
        try:
            # URL-encode the container path for the URL
            encoded_path = quote(container_path, safe='/')
            xml_url = f"https://{server}{encoded_path}/testresults-viewXml.view?runid={run_id}"

            logger.info(f"Fetching XML from: {xml_url}")

            # Make authenticated HTTP request
            response_bytes = make_authenticated_request(server, xml_url, timeout=120)
            response_text = response_bytes.decode("utf-8")

            # Parse JSON response - endpoint returns {xml: "..."}
            data = json.loads(response_text)
            xml_content = data.get("xml", "")

            if not xml_content:
                return f"Test run #{run_id} has no XML content"

            # Save to tmp directory
            output_dir = get_tmp_dir()
            output_file = output_dir / f"testrun-xml-{run_id}.xml"
            output_file.write_text(xml_content, encoding="utf-8")

            # Calculate metadata
            size_bytes = output_file.stat().st_size
            line_count = xml_content.count("\n") + 1

            return (
                f"XML saved successfully:\n"
                f"  file_path: {output_file}\n"
                f"  size_bytes: {size_bytes:,}\n"
                f"  line_count: {line_count:,}\n"
                f"\nUse Grep or Read tools to search within this file."
            )

        except Exception as e:
            logger.error(f"Error saving run XML: {e}", exc_info=True)
            return f"Error saving run XML: {e}"

    @mcp.tool()
    async def get_run_leaks(
        run_id: int,
        server: str = DEFAULT_SERVER,
        container_path: str = DEFAULT_TEST_CONTAINER,
    ) -> str:
        """**DRILL-DOWN**: Get memory/handle leaks for a specific run.

        Args:
            run_id: Test run ID from get_daily_test_summary
            container_path: Test folder (must match the run's folder)
        """
        try:
            server_context = get_server_context(server, container_path)
            filter_array = [QueryFilter("testrunid", str(run_id), "eq")]

            # Query both memory leaks and handle leaks
            mem_result = labkey.query.select_rows(
                server_context=server_context,
                schema_name=TESTRESULTS_SCHEMA,
                query_name="memoryleaks",
                max_rows=100,
                filter_array=filter_array,
            )

            handle_result = labkey.query.select_rows(
                server_context=server_context,
                schema_name=TESTRESULTS_SCHEMA,
                query_name="handleleaks",
                max_rows=100,
                filter_array=filter_array,
            )

            lines = [f"Leaks for run #{run_id}:", ""]

            mem_rows = mem_result.get("rows", []) if mem_result else []
            handle_rows = handle_result.get("rows", []) if handle_result else []

            if mem_rows:
                lines.append(f"=== Memory Leaks ({len(mem_rows)}) ===")
                for row in mem_rows:
                    testname = row.get("testname", "Unknown")
                    bytes_leaked = row.get("bytes", 0)
                    lines.append(f"  {testname}: {bytes_leaked:,} bytes")
                lines.append("")

            if handle_rows:
                lines.append(f"=== Handle Leaks ({len(handle_rows)}) ===")
                for row in handle_rows:
                    testname = row.get("testname", "Unknown")
                    handles = row.get("handles", row.get("count", "?"))
                    lines.append(f"  {testname}: {handles} handles")
                lines.append("")

            if not mem_rows and not handle_rows:
                return f"No leaks found for run #{run_id}"

            return "\n".join(lines)

        except Exception as e:
            logger.error(f"Error getting run leaks: {e}", exc_info=True)
            return f"Error getting run leaks: {e}"

    @mcp.tool()
    async def get_daily_test_summary(
        report_date: str,
        server: str = DEFAULT_SERVER,
    ) -> str:
        """**PRIMARY**: Daily nightly test report. Queries all 6 folders. Saves to ai/.tmp/nightly-report-YYYYMMDD.md.

        Args:
            report_date: Date YYYY-MM-DD (end of 8AM-8AM nightly window)
        """
        # Parse report_date as the END of the nightly window
        # Nightly "day" runs from 8:01 AM day before to 8:00 AM report_date
        end_dt = datetime.strptime(report_date, "%Y-%m-%d")
        start_dt = end_dt - timedelta(days=1)

        # Define the 8AM boundaries for filtering
        window_start = start_dt.replace(hour=8, minute=1, second=0, microsecond=0)
        window_end = end_dt.replace(hour=8, minute=0, second=0, microsecond=0)

        # Query both calendar dates from server (will filter by time later)
        start_date = start_dt.strftime("%Y-%m-%d")
        end_date = end_dt.strftime("%Y-%m-%d")

        # All 6 test folders with their expected durations
        folders = [
            ("/home/development/Nightly x64", 540),
            ("/home/development/Release Branch", 540),
            ("/home/development/Performance Tests", 720),
            ("/home/development/Release Branch Performance Tests", 720),
            ("/home/development/Integration", 540),
            ("/home/development/Integration with Perf Tests", 720),
        ]

        all_results = []
        summary_lines = [
            f"# Daily Test Summary: {report_date}",
            "",
            "## Summary by Folder",
            "",
            "| Folder | Runs | Pass | Fail | Leak | Anomaly |",
            "|--------|------|------|------|------|---------|",
        ]

        total_tests = 0
        runs_with_failures = []
        runs_with_leaks = []
        runs_with_hangs = []

        for container_path, expected_duration in folders:
            folder_name = container_path.split("/")[-1]
            try:
                server_context = get_server_context(server, container_path)

                # Query expected computers with their trained values
                expected_result = labkey.query.select_rows(
                    server_context=server_context,
                    schema_name=TESTRESULTS_SCHEMA,
                    query_name="expected_computers",
                    max_rows=100,
                )
                expected_computers = {}
                if expected_result and expected_result.get("rows"):
                    for ec in expected_result["rows"]:
                        comp_name = ec.get("computer", "")
                        if comp_name:
                            expected_computers[comp_name] = {
                                "meantestsrun": ec.get("meantestsrun", 0),
                                "stddevtestsrun": ec.get("stddevtestsrun", 1),
                                "meanmemory": ec.get("meanmemory", 0),
                                "stddevmemory": ec.get("stddevmemory", 1),
                            }

                result = labkey.query.select_rows(
                    server_context=server_context,
                    schema_name=TESTRESULTS_SCHEMA,
                    query_name="testruns_detail",
                    max_rows=50,
                    parameters={"StartDate": start_date, "EndDate": end_date},
                    sort="-posttime",  # API sort required - ORDER BY in SQL unreliable
                )

                rows = result.get("rows", []) if result else []

                # Filter rows by posttime within the 8AM-8AM window
                filtered_rows = []
                for row in rows:
                    posttime_str = row.get("posttime", "")
                    if posttime_str:
                        try:
                            # Parse posttime (format: "2025-12-16 21:07:00.000" or similar)
                            posttime_dt = datetime.strptime(str(posttime_str)[:19], "%Y-%m-%d %H:%M:%S")
                            if window_start <= posttime_dt <= window_end:
                                filtered_rows.append(row)
                        except ValueError:
                            # If parsing fails, include the row (be permissive)
                            filtered_rows.append(row)
                rows = filtered_rows

                if not rows:
                    summary_lines.append(f"| {folder_name} | 0 | - | - | - | - |")
                    continue

                # Analyze runs
                pass_count = 0
                fail_count = 0
                leak_count = 0
                anomaly_count = 0
                computers_seen = set()

                folder_data = {
                    "folder": folder_name,
                    "container_path": container_path,
                    "runs": [],
                    "missing_computers": [],
                }

                for row in rows:
                    run_id = row.get("run_id", "?")
                    computer = row.get("computer", "?")
                    duration = row.get("duration", 0)
                    passed = row.get("passedtests", 0)
                    failed = row.get("failedtests", 0)
                    leaked = row.get("leakedtests", 0)
                    githash = row.get("githash", "?")
                    posttime = row.get("posttime", "?")
                    averagemem = row.get("averagemem", 0)
                    hung_test = row.get("hung_test")
                    hung_language = row.get("hung_language")

                    computers_seen.add(computer)
                    total_tests += passed

                    # Classify run using trained stddev if available
                    is_anomaly = False
                    anomaly_reason = ""

                    if computer in expected_computers:
                        ec = expected_computers[computer]
                        mean_tests = ec["meantestsrun"]
                        stddev_tests = ec["stddevtestsrun"]
                        if stddev_tests > 0:
                            z_score = abs(passed - mean_tests) / stddev_tests
                            if z_score >= 4:
                                is_anomaly = True
                                anomaly_reason = f"4σ ({passed} vs {mean_tests:.0f})"
                            elif z_score >= 3:
                                is_anomaly = True
                                anomaly_reason = f"3σ ({passed} vs {mean_tests:.0f})"

                    # Fallback: Short duration = crash
                    if not is_anomaly and duration < expected_duration * 0.9:
                        is_anomaly = True
                        anomaly_reason = f"short ({duration} min)"

                    if failed > 0:
                        fail_count += 1
                        runs_with_failures.append({
                            "run_id": run_id,
                            "folder": folder_name,
                            "container_path": container_path,
                            "computer": computer,
                            "failed": failed
                        })
                    elif leaked > 0:
                        leak_count += 1
                        runs_with_leaks.append({
                            "run_id": run_id,
                            "folder": folder_name,
                            "container_path": container_path,
                            "computer": computer,
                            "leaked": leaked
                        })
                    elif is_anomaly:
                        anomaly_count += 1
                    else:
                        pass_count += 1

                    # Track hangs separately (a run can have both failures and hangs)
                    if hung_test:
                        runs_with_hangs.append({
                            "run_id": run_id,
                            "folder": folder_name,
                            "container_path": container_path,
                            "computer": computer,
                            "hung_test": hung_test,
                            "hung_language": hung_language,
                        })

                    folder_data["runs"].append({
                        "run_id": run_id,
                        "computer": computer,
                        "posttime": str(posttime)[:16] if posttime else "?",
                        "duration": duration,
                        "passed": passed,
                        "failed": failed,
                        "leaked": leaked,
                        "memory": averagemem,
                        "githash": str(githash)[:9] if githash else "?",
                        "anomaly": anomaly_reason,
                        "hung_test": hung_test,
                        "hung_language": hung_language,
                    })

                # Find missing computers
                missing = set(expected_computers.keys()) - computers_seen
                folder_data["missing_computers"] = sorted(missing)

                all_results.append(folder_data)

                # Include missing count in anomaly column
                missing_count = len(missing)
                anomaly_display = anomaly_count
                if missing_count > 0:
                    anomaly_display = f"{anomaly_count}+{missing_count}m"

                summary_lines.append(
                    f"| {folder_name} | {len(rows)} | {pass_count} | {fail_count} | {leak_count} | {anomaly_display} |"
                )

            except Exception as e:
                logger.error(f"Error querying {folder_name}: {e}")
                summary_lines.append(f"| {folder_name} | ERROR | - | - | - | - |")

        summary_lines.extend([
            "",
            f"**Total tests run**: {total_tests:,}",
            "",
        ])

        # Add details for each folder with data
        for folder_data in all_results:
            if not folder_data["runs"]:
                continue

            summary_lines.extend([
                f"## {folder_data['folder']}",
                "",
                "| Computer | Memory | Tests | PostTime | Duration | Fail | Leak | Hung | Git Hash | Anomaly |",
                "|----------|--------|-------|----------|----------|------|------|------|----------|---------|",
            ])

            for run in folder_data["runs"]:
                hung_display = run.get('hung_test', '') or ''
                if hung_display and run.get('hung_language'):
                    hung_display = f"{hung_display} ({run['hung_language']})"
                summary_lines.append(
                    f"| {run['computer']} | {run['memory']} | {run['passed']} | {run['posttime']} | "
                    f"{run['duration']} | {run['failed']} | {run['leaked']} | {hung_display} | {run['githash']} | {run['anomaly']} |"
                )

            # Add missing computers if any
            if folder_data.get("missing_computers"):
                summary_lines.append("")
                summary_lines.append(f"**Missing**: {', '.join(folder_data['missing_computers'])}")

            summary_lines.append("")

        # Query failures and leaks by test name for each folder with issues
        all_failures = []  # (testname, computer, folder)
        all_leaks = []  # (testname, computer, folder, leak_type)

        for folder_data in all_results:
            if not folder_data["runs"]:
                continue

            container_path = folder_data["container_path"]
            folder_name = folder_data["folder"]

            # Check if this folder had any failures or leaks
            has_failures = any(r["failed"] > 0 for r in folder_data["runs"])
            has_leaks = any(r["leaked"] > 0 for r in folder_data["runs"])

            try:
                server_context = get_server_context(server, container_path)

                if has_failures:
                    fail_result = labkey.query.select_rows(
                        server_context=server_context,
                        schema_name=TESTRESULTS_SCHEMA,
                        query_name="failures_by_date",
                        max_rows=100,
                        parameters={"StartDate": start_date, "EndDate": end_date},
                    )
                    if fail_result and fail_result.get("rows"):
                        for row in fail_result["rows"]:
                            all_failures.append({
                                "testname": row.get("testname", "?"),
                                "computer": row.get("computer", "?"),
                                "folder": folder_name,
                            })

                if has_leaks:
                    leak_result = labkey.query.select_rows(
                        server_context=server_context,
                        schema_name=TESTRESULTS_SCHEMA,
                        query_name="leaks_by_date",
                        max_rows=100,
                        parameters={"StartDate": start_date, "EndDate": end_date},
                    )
                    if leak_result and leak_result.get("rows"):
                        for row in leak_result["rows"]:
                            all_leaks.append({
                                "testname": row.get("testname", "?"),
                                "computer": row.get("computer", "?"),
                                "folder": folder_name,
                                "leak_type": row.get("leak_type", "?"),
                            })
            except Exception as e:
                logger.error(f"Error querying failures/leaks for {folder_name}: {e}")

        # Group failures by test name
        if all_failures:
            summary_lines.extend([
                "## Failures by Test",
                "",
                "| Test | Computers | Folder |",
                "|------|-----------|--------|",
            ])
            # Group by (testname, folder)
            failure_groups = defaultdict(list)
            for f in all_failures:
                key = (f["testname"], f["folder"])
                failure_groups[key].append(f["computer"])

            for (testname, folder), computers in sorted(failure_groups.items()):
                computers_str = ", ".join(sorted(set(computers)))
                summary_lines.append(f"| {testname} | {computers_str} | {folder} |")
            summary_lines.append("")

        # Group leaks by test name
        if all_leaks:
            summary_lines.extend([
                "## Leaks by Test",
                "",
                "| Test | Type | Computers | Folder |",
                "|------|------|-----------|--------|",
            ])
            # Group by (testname, folder, leak_type)
            leak_groups = defaultdict(list)
            for l in all_leaks:
                key = (l["testname"], l["folder"], l["leak_type"])
                leak_groups[key].append(l["computer"])

            for (testname, folder, leak_type), computers in sorted(leak_groups.items()):
                computers_str = ", ".join(sorted(set(computers)))
                summary_lines.append(f"| {testname} | {leak_type} | {computers_str} | {folder} |")
            summary_lines.append("")

        # Group hangs by test name
        if runs_with_hangs:
            summary_lines.extend([
                "## Hangs by Test",
                "",
                "| Test | Language | Computers | Folder |",
                "|------|----------|-----------|--------|",
            ])
            # Group by (testname, folder, language)
            hang_groups = defaultdict(list)
            for h in runs_with_hangs:
                key = (h["hung_test"], h["folder"], h.get("hung_language", "?"))
                hang_groups[key].append(h["computer"])

            for (testname, folder, language), computers in sorted(hang_groups.items()):
                computers_str = ", ".join(sorted(set(computers)))
                summary_lines.append(f"| {testname} | {language} | {computers_str} | {folder} |")
            summary_lines.append("")

        # Write full report to file
        report_content = "\n".join(summary_lines)

        output_dir = get_tmp_dir()

        # Use report_date for filename
        date_str = report_date.replace("-", "")
        output_file = output_dir / f"nightly-report-{date_str}.md"
        output_file.write_text(report_content, encoding="utf-8")

        # Build brief summary for return
        total_runs = sum(len(fd["runs"]) for fd in all_results)
        total_failures = len(runs_with_failures)
        total_leaks = len(runs_with_leaks)
        total_hangs = len(runs_with_hangs)
        total_missing = sum(len(fd.get("missing_computers", [])) for fd in all_results)

        brief = [
            f"Daily test report saved to: {output_file}",
            "",
            f"Summary: {total_runs} runs across {len([f for f in all_results if f['runs']])} folders",
            f"  - Total tests: {total_tests:,}",
            f"  - Runs with failures: {total_failures}",
            f"  - Runs with leaks: {total_leaks}",
            f"  - Runs with hangs: {total_hangs}",
            f"  - Missing computers: {total_missing}",
            "",
        ]

        if runs_with_failures:
            brief.append("Failures to investigate:")
            for r in runs_with_failures:
                brief.append(f"  - get_run_failures({r['run_id']})  # {r['folder']}/{r['computer']}")

        if runs_with_leaks:
            brief.append("Leaks to investigate:")
            for r in runs_with_leaks:
                brief.append(f"  - get_run_leaks({r['run_id']})  # {r['folder']}/{r['computer']}")

        if runs_with_hangs:
            brief.append("Hangs to investigate:")
            for h in runs_with_hangs:
                brief.append(f"  - {h['hung_test']} ({h.get('hung_language', '?')})  # {h['folder']}/{h['computer']}")

        brief.append("")
        brief.append(f"See {output_file} for full details.")

        return "\n".join(brief)

    @mcp.tool()
    async def save_test_failure_history(
        test_name: str,
        start_date: str,
        end_date: Optional[str] = None,
        server: str = DEFAULT_SERVER,
        container_path: str = DEFAULT_TEST_CONTAINER,
    ) -> str:
        """**DRILL-DOWN**: Compare stack traces for recurring failures. Saves to ai/.tmp/test-failures-{testname}.md.

        Args:
            test_name: Test name (e.g., "TestPanoramaDownloadFile")
            start_date: Start date YYYY-MM-DD
            end_date: End date YYYY-MM-DD (default: same as start_date)
            container_path: Test folder to search
        """
        # Use same date for start and end if not specified (single day query)
        if not end_date:
            end_date = start_date
        folder_name = container_path.split("/")[-1]

        all_failures = []

        try:
            server_context = get_server_context(server, container_path)

            # Get failures for this test in the date range
            fail_result = labkey.query.select_rows(
                server_context=server_context,
                schema_name=TESTRESULTS_SCHEMA,
                query_name="failures_by_date",
                max_rows=500,
                parameters={"StartDate": start_date, "EndDate": end_date},
            )

            if not fail_result or not fail_result.get("rows"):
                return f"No failures found in '{folder_name}' for {start_date} to {end_date}."

            # Filter for our specific test and collect run IDs
            matching_runs = {}
            for row in fail_result["rows"]:
                if row.get("testname") == test_name:
                    run_id = row.get("testrunid")
                    computer = row.get("computer", "?")
                    posttime = row.get("posttime", "?")
                    if run_id:
                        matching_runs[run_id] = {
                            "computer": computer,
                            "posttime": str(posttime)[:16] if posttime else "?",
                        }

            if not matching_runs:
                return f"No failures found for test '{test_name}' in '{folder_name}' ({start_date} to {end_date})."

            # Get the full stack traces from testfails for each run
            for run_id, run_info in matching_runs.items():
                try:
                    stack_result = labkey.query.select_rows(
                        server_context=server_context,
                        schema_name=TESTRESULTS_SCHEMA,
                        query_name="testfails",
                        max_rows=10,
                        filter_array=[
                            QueryFilter("testrunid", str(run_id), "eq"),
                            QueryFilter("testname", test_name, "eq"),
                        ],
                    )

                    if stack_result and stack_result.get("rows"):
                        for row in stack_result["rows"]:
                            all_failures.append({
                                "run_id": run_id,
                                "computer": run_info["computer"],
                                "posttime": run_info["posttime"],
                                "pass_num": row.get("pass", "?"),
                                "stacktrace": row.get("stacktrace", "No stack trace"),
                            })
                except Exception as e:
                    logger.error(f"Error getting stack trace for run {run_id}: {e}")

        except Exception as e:
            logger.error(f"Error querying {folder_name}: {e}")
            return f"Error querying {folder_name}: {e}"

        if not all_failures:
            return f"No failures found for test '{test_name}' in '{folder_name}' ({start_date} to {end_date})."

        # Build the report
        lines = [
            f"# Failure History: {test_name}",
            f"",
            f"**Folder**: {folder_name}",
            f"**Date range**: {start_date} to {end_date}",
            f"**Total failures**: {len(all_failures)}",
            f"",
            "## Summary",
            "",
            "| Computer | Date | Pass |",
            "|----------|------|------|",
        ]

        for f in all_failures:
            lines.append(f"| {f['computer']} | {f['posttime']} | {f['pass_num']} |")

        lines.extend(["", "## Stack Traces", ""])

        # Group by unique stack traces to identify patterns
        trace_groups = defaultdict(list)
        for f in all_failures:
            # Normalize stack trace for grouping (first 500 chars as key)
            trace_key = f["stacktrace"][:500] if f["stacktrace"] else "empty"
            trace_groups[trace_key].append(f)

        lines.append(f"**Unique stack trace patterns**: {len(trace_groups)}")
        lines.append("")

        if len(trace_groups) == 1:
            lines.append("All failures have the **same stack trace pattern** - likely same root cause.")
            lines.append("")

        # Output each unique pattern
        pattern_num = 0
        for trace_key, failures in trace_groups.items():
            pattern_num += 1
            computers = sorted(set(f["computer"] for f in failures))

            lines.extend([
                f"### Pattern {pattern_num} ({len(failures)} occurrences)",
                f"",
                f"**Computers**: {', '.join(computers)}",
                f"",
                "```",
                failures[0]["stacktrace"],
                "```",
                "",
            ])

        # Write to file
        report_content = "\n".join(lines)

        output_dir = get_tmp_dir()

        # Sanitize test name for filename
        safe_name = test_name.replace("/", "_").replace("\\", "_").replace(" ", "_")
        output_file = output_dir / f"test-failures-{safe_name}.md"
        output_file.write_text(report_content, encoding="utf-8")

        # Build brief summary
        unique_patterns = len(trace_groups)
        pattern_msg = "same root cause" if unique_patterns == 1 else f"{unique_patterns} different patterns"

        return (
            f"Failure history saved to: {output_file}\n"
            f"\n"
            f"Summary for '{test_name}' in {folder_name} ({start_date} to {end_date}):\n"
            f"  - Total failures: {len(all_failures)}\n"
            f"  - Unique stack trace patterns: {unique_patterns} ({pattern_msg})\n"
            f"  - Computers affected: {', '.join(sorted(set(f['computer'] for f in all_failures)))}\n"
            f"\n"
            f"See {output_file} for full stack traces."
        )

    @mcp.tool()
    async def save_daily_failures(
        report_date: str,
        server: str = DEFAULT_SERVER,
    ) -> str:
        """**PRIMARY**: All test failures with stack traces for a date. Saves to ai/.tmp/failures-YYYYMMDD.md.

        Queries all 6 test folders, normalizes stack traces, and groups by fingerprint
        to identify unique bugs vs duplicate reports.

        Args:
            report_date: Date YYYY-MM-DD (end of 8AM-8AM nightly window)

        Returns:
            Summary with fingerprint groupings. Full details in saved file.
        """
        # Parse report_date as the END of the nightly window
        # Nightly "day" runs from 8:01 AM day before to 8:00 AM report_date
        end_dt = datetime.strptime(report_date, "%Y-%m-%d")
        start_dt = end_dt - timedelta(days=1)

        # Define the 8AM boundaries
        window_start = start_dt.replace(hour=8, minute=1, second=0)
        window_end = end_dt.replace(hour=8, minute=0, second=0)

        # Format for LabKey TIMESTAMP parameters
        window_start_str = window_start.strftime("%Y-%m-%d %H:%M:%S")
        window_end_str = window_end.strftime("%Y-%m-%d %H:%M:%S")

        # All 6 test folders
        folders = [
            "/home/development/Nightly x64",
            "/home/development/Release Branch",
            "/home/development/Performance Tests",
            "/home/development/Release Branch Performance Tests",
            "/home/development/Integration",
            "/home/development/Integration with Perf Tests",
        ]

        all_failures = []

        for container_path in folders:
            folder_name = container_path.split("/")[-1]
            try:
                server_context = get_server_context(server, container_path)

                result = labkey.query.select_rows(
                    server_context=server_context,
                    schema_name=TESTRESULTS_SCHEMA,
                    query_name="failures_with_traces_by_date",
                    max_rows=500,
                    parameters={
                        "WindowStart": window_start_str,
                        "WindowEnd": window_end_str,
                    },
                )

                if result and result.get("rows"):
                    for row in result["rows"]:
                        all_failures.append({
                            "testname": row.get("testname", "?"),
                            "computer": row.get("computer", "?"),
                            "folder": folder_name,
                            "posttime": str(row.get("posttime", "?"))[:16],
                            "passnum": row.get("passnum", "?"),
                            "stacktrace": row.get("stacktrace", ""),
                        })

            except Exception as e:
                logger.error(f"Error querying {folder_name}: {e}")

        if not all_failures:
            return f"No failures found for {report_date} (window: {window_start_str} to {window_end_str})"

        # Normalize stack traces and group by fingerprint
        traces = [f["stacktrace"] for f in all_failures]
        fingerprint_groups = group_by_fingerprint(traces)

        # Build fingerprint -> failures mapping
        fingerprint_data = {}
        for fingerprint, indices in fingerprint_groups.items():
            failures_in_group = [all_failures[i] for i in indices]
            # Get normalized info from first trace
            norm = normalize_stack_trace(failures_in_group[0]["stacktrace"])
            fingerprint_data[fingerprint] = {
                "failures": failures_in_group,
                "signature": norm.signature_frames,
                "normalized": norm.normalized,
                "count": len(failures_in_group),
            }

        # Sort by count (most common first)
        sorted_fingerprints = sorted(
            fingerprint_data.items(),
            key=lambda x: -x[1]["count"]
        )

        # Build the report
        lines = [
            f"# Daily Failures Report: {report_date}",
            "",
            f"**Window**: {window_start_str} to {window_end_str}",
            f"**Total failures**: {len(all_failures)}",
            f"**Unique bugs (by fingerprint)**: {len(fingerprint_groups)}",
            "",
            "## Summary by Fingerprint",
            "",
            "| Fingerprint | Count | Tests | Computers | Signature |",
            "|-------------|-------|-------|-----------|-----------|",
        ]

        for fingerprint, data in sorted_fingerprints:
            tests = sorted(set(f["testname"] for f in data["failures"]))
            computers = sorted(set(f["computer"] for f in data["failures"]))
            sig = " → ".join(data["signature"][:3]) if data["signature"] else "(no signature)"
            lines.append(
                f"| {fingerprint} | {data['count']} | {', '.join(tests[:2])}{'...' if len(tests) > 2 else ''} | "
                f"{', '.join(computers[:3])}{'...' if len(computers) > 3 else ''} | {sig} |"
            )

        lines.extend(["", "## Details by Fingerprint", ""])

        # Detailed section for each fingerprint
        for fingerprint, data in sorted_fingerprints:
            tests = sorted(set(f["testname"] for f in data["failures"]))
            computers = sorted(set(f["computer"] for f in data["failures"]))
            folders_hit = sorted(set(f["folder"] for f in data["failures"]))

            lines.extend([
                f"### Fingerprint: {fingerprint}",
                "",
                f"**Count**: {data['count']} failures",
                f"**Tests**: {', '.join(tests)}",
                f"**Computers**: {', '.join(computers)}",
                f"**Folders**: {', '.join(folders_hit)}",
                "",
                "**Signature frames**:",
                "```",
            ])
            for frame in data["signature"]:
                lines.append(f"  {frame}")
            lines.extend([
                "```",
                "",
                "**Normalized stack trace**:",
                "```",
                data["normalized"] if data["normalized"] else "(empty)",
                "```",
                "",
                "**Raw stack trace (first occurrence)**:",
                "```",
                data["failures"][0]["stacktrace"][:2000] if data["failures"][0]["stacktrace"] else "(empty)",
                "```",
                "",
            ])

        # Write to file
        report_content = "\n".join(lines)
        output_dir = get_tmp_dir()
        date_str = report_date.replace("-", "")
        output_file = output_dir / f"failures-{date_str}.md"
        output_file.write_text(report_content, encoding="utf-8")

        # Build brief summary
        brief_lines = [
            f"Daily failures saved to: {output_file}",
            "",
            f"**{report_date}** (8AM window): {len(all_failures)} failures, {len(fingerprint_groups)} unique bugs",
            "",
        ]

        if len(sorted_fingerprints) <= 5:
            brief_lines.append("Fingerprints:")
            for fingerprint, data in sorted_fingerprints:
                tests = sorted(set(f["testname"] for f in data["failures"]))
                computers = sorted(set(f["computer"] for f in data["failures"]))
                brief_lines.append(
                    f"  - {fingerprint}: {data['count']}x on {len(computers)} machines - {', '.join(tests[:2])}"
                )
        else:
            brief_lines.append(f"Top fingerprints (of {len(sorted_fingerprints)}):")
            for fingerprint, data in sorted_fingerprints[:5]:
                tests = sorted(set(f["testname"] for f in data["failures"]))
                computers = sorted(set(f["computer"] for f in data["failures"]))
                brief_lines.append(
                    f"  - {fingerprint}: {data['count']}x on {len(computers)} machines - {', '.join(tests[:2])}"
                )

        brief_lines.extend(["", f"See {output_file} for full details."])

        return "\n".join(brief_lines)

    @mcp.tool()
    async def save_run_comparison(
        run_id_before: int,
        run_id_after: int,
        server: str = DEFAULT_SERVER,
        container_path: str = DEFAULT_TEST_CONTAINER,
    ) -> str:
        """**ANALYSIS**: Compare test durations between two runs. Saves to ai/.tmp/run-comparison-{before}-{after}.md.

        Identifies tests that got slower, faster, were added, or were removed.
        Essential for diagnosing why test counts or durations changed between runs.

        Use case: When daily report shows a significant drop in test count or
        anomalous duration, compare a "good" baseline run with the "changed" run
        to identify which tests are responsible.

        Args:
            run_id_before: Baseline run ID (the "good" state)
            run_id_after: Comparison run ID (the "changed" state)
            container_path: Test folder (must match where the runs are located)

        Returns:
            Summary of timing differences with detailed breakdown in saved file.
        """
        folder_name = container_path.split("/")[-1]

        try:
            server_context = get_server_context(server, container_path)

            result = labkey.query.select_rows(
                server_context=server_context,
                schema_name=TESTRESULTS_SCHEMA,
                query_name="compare_run_timings",
                max_rows=1000,
                parameters={
                    "RunIdBefore": str(run_id_before),
                    "RunIdAfter": str(run_id_after),
                },
            )

            if not result or not result.get("rows"):
                return f"No timing data found for runs {run_id_before} vs {run_id_after} in {folder_name}"

            rows = result["rows"]

            # Categorize tests
            new_tests = []      # Only in after (before duration is None/0)
            gone_tests = []     # Only in before (after duration is None/0)
            slowdowns = []      # Got significantly slower (>50% or >10s delta)
            speedups = []       # Got significantly faster
            unchanged = []      # Minor changes

            total_delta = 0
            total_before = 0
            total_after = 0

            for row in rows:
                testname = row.get("testname", "?")
                passes = row.get("passes") or 0
                duration_before = row.get("duration_before")
                duration_after = row.get("duration_after")
                delta_avg = row.get("delta_avg") or 0
                delta_total = row.get("delta_total") or 0
                delta_percent = row.get("delta_percent")

                # Handle None values
                before_val = duration_before if duration_before is not None else 0
                after_val = duration_after if duration_after is not None else 0

                total_before += before_val * passes if before_val else 0
                total_after += after_val * passes if after_val else 0
                total_delta += delta_total

                test_data = {
                    "testname": testname,
                    "passes": passes,
                    "before": before_val,
                    "after": after_val,
                    "delta_avg": delta_avg,
                    "delta_total": delta_total,
                    "delta_percent": delta_percent,
                }

                if duration_before is None or duration_before == 0:
                    if duration_after and duration_after > 0:
                        new_tests.append(test_data)
                elif duration_after is None or duration_after == 0:
                    gone_tests.append(test_data)
                elif delta_percent is not None and delta_percent > 50 and delta_avg > 1:
                    slowdowns.append(test_data)
                elif delta_percent is not None and delta_percent < -20 and delta_avg < -1:
                    speedups.append(test_data)
                else:
                    unchanged.append(test_data)

            # Sort by delta_total (biggest impact first for slowdowns, biggest savings for speedups)
            slowdowns.sort(key=lambda x: -x["delta_total"])
            speedups.sort(key=lambda x: x["delta_total"])
            new_tests.sort(key=lambda x: -x["after"] * x["passes"])
            gone_tests.sort(key=lambda x: -x["before"] * x["passes"])

            # Build the report
            lines = [
                f"# Run Timing Comparison: {run_id_before} vs {run_id_after}",
                "",
                f"**Folder**: {folder_name}",
                f"**Baseline run**: {run_id_before}",
                f"**Comparison run**: {run_id_after}",
                "",
                "## Summary",
                "",
                f"| Metric | Value |",
                f"|--------|-------|",
                f"| Total time before | {total_before:,.0f}s ({total_before/3600:.1f}h) |",
                f"| Total time after | {total_after:,.0f}s ({total_after/3600:.1f}h) |",
                f"| Net change | {total_delta:+,.0f}s ({total_delta/60:+,.0f}m) |",
                f"| Tests added | {len(new_tests)} |",
                f"| Tests removed | {len(gone_tests)} |",
                f"| Tests slowed (>50%) | {len(slowdowns)} |",
                f"| Tests sped up (>20%) | {len(speedups)} |",
                "",
            ]

            # Major slowdowns section
            if slowdowns:
                lines.extend([
                    "## Major Slowdowns (>50% slower)",
                    "",
                    "| Test | Passes | Before | After | Delta | Total Impact | % Change |",
                    "|------|--------|--------|-------|-------|--------------|----------|",
                ])
                for t in slowdowns[:20]:
                    pct = f"+{t['delta_percent']:.0f}%" if t['delta_percent'] else "N/A"
                    lines.append(
                        f"| {t['testname'][:45]} | {t['passes']} | {t['before']:.0f}s | {t['after']:.0f}s | "
                        f"+{t['delta_avg']:.0f}s | +{t['delta_total']:.0f}s | {pct} |"
                    )
                if len(slowdowns) > 20:
                    lines.append(f"| ... and {len(slowdowns) - 20} more | | | | | | |")
                lines.append("")

            # New tests section
            if new_tests:
                lines.extend([
                    "## New Tests (not in baseline)",
                    "",
                    "| Test | Passes | Duration | Total Time |",
                    "|------|--------|----------|------------|",
                ])
                for t in new_tests[:20]:
                    total_time = t['after'] * t['passes']
                    lines.append(
                        f"| {t['testname'][:50]} | {t['passes']} | {t['after']:.0f}s | {total_time:.0f}s |"
                    )
                if len(new_tests) > 20:
                    lines.append(f"| ... and {len(new_tests) - 20} more | | | |")
                lines.append("")

            # Gone tests section
            if gone_tests:
                lines.extend([
                    "## Removed Tests (not in comparison)",
                    "",
                    "| Test | Passes | Duration | Time Saved |",
                    "|------|--------|----------|------------|",
                ])
                for t in gone_tests[:20]:
                    total_time = t['before'] * t['passes']
                    lines.append(
                        f"| {t['testname'][:50]} | {t['passes']} | {t['before']:.0f}s | -{total_time:.0f}s |"
                    )
                if len(gone_tests) > 20:
                    lines.append(f"| ... and {len(gone_tests) - 20} more | | | |")
                lines.append("")

            # Speedups section
            if speedups:
                lines.extend([
                    "## Major Speedups (>20% faster)",
                    "",
                    "| Test | Passes | Before | After | Delta | Total Saved | % Change |",
                    "|------|--------|--------|-------|-------|-------------|----------|",
                ])
                for t in speedups[:10]:
                    pct = f"{t['delta_percent']:.0f}%" if t['delta_percent'] else "N/A"
                    lines.append(
                        f"| {t['testname'][:45]} | {t['passes']} | {t['before']:.0f}s | {t['after']:.0f}s | "
                        f"{t['delta_avg']:.0f}s | {t['delta_total']:.0f}s | {pct} |"
                    )
                lines.append("")

            # Top impact analysis
            lines.extend([
                "## Top Time Impact Analysis",
                "",
                "The following tests contributed most to the time change:",
                "",
            ])

            # Combine all tests and sort by absolute delta_total
            all_changes = slowdowns + speedups + new_tests + gone_tests
            all_changes.sort(key=lambda x: abs(x["delta_total"]), reverse=True)

            for i, t in enumerate(all_changes[:5], 1):
                if t in new_tests:
                    impact_type = "NEW"
                    desc = f"added {t['after']:.0f}s × {t['passes']} passes"
                elif t in gone_tests:
                    impact_type = "REMOVED"
                    desc = f"saved {t['before']:.0f}s × {t['passes']} passes"
                elif t["delta_total"] > 0:
                    impact_type = "SLOWER"
                    desc = f"{t['before']:.0f}s → {t['after']:.0f}s (+{t['delta_percent']:.0f}%)"
                else:
                    impact_type = "FASTER"
                    desc = f"{t['before']:.0f}s → {t['after']:.0f}s ({t['delta_percent']:.0f}%)"

                lines.append(f"{i}. **{t['testname']}** [{impact_type}]: {desc} = {t['delta_total']:+,.0f}s total")

            lines.append("")

            # Write to file
            report_content = "\n".join(lines)
            output_dir = get_tmp_dir()
            output_file = output_dir / f"run-comparison-{run_id_before}-{run_id_after}.md"
            output_file.write_text(report_content, encoding="utf-8")

            # Build brief summary for return
            brief = [
                f"Run comparison saved to: {output_file}",
                "",
                f"**{folder_name}**: Run {run_id_before} vs {run_id_after}",
                f"  Net time change: {total_delta:+,.0f}s ({total_delta/60:+,.0f} minutes)",
                f"  Tests added: {len(new_tests)}, removed: {len(gone_tests)}",
                f"  Slowdowns (>50%): {len(slowdowns)}, Speedups (>20%): {len(speedups)}",
                "",
            ]

            if all_changes:
                brief.append("Top impacts:")
                for t in all_changes[:3]:
                    if t in new_tests:
                        brief.append(f"  - {t['testname']}: NEW (+{t['after'] * t['passes']:.0f}s)")
                    elif t in gone_tests:
                        brief.append(f"  - {t['testname']}: REMOVED (-{t['before'] * t['passes']:.0f}s)")
                    else:
                        brief.append(f"  - {t['testname']}: {t['delta_total']:+,.0f}s ({t['delta_percent']:+.0f}%)")

            brief.extend(["", f"See {output_file} for full details."])

            return "\n".join(brief)

        except Exception as e:
            logger.error(f"Error comparing run timings: {e}", exc_info=True)
            return f"Error comparing run timings: {e}"
