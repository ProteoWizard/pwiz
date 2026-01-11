# Daily Report Guide

Comprehensive guide for generating daily consolidated reports covering nightly tests, exceptions, and support activity.

## Overview

The daily report consolidates three data sources:
1. **Nightly test results** - All 6 test folders (Nightly x64, Release Branch, etc.)
2. **User-submitted exceptions** - Crash reports from skyline.ms
3. **Support board activity** - Unanswered threads needing attention

## Effort Levels

| Level | Duration | Scope |
|-------|----------|-------|
| `quick` | ~1-2 min | Generate report, send email, minimal investigation |
| `standard` | ~15-30 min | Report + investigate top issues, git blame for regressions, update TODO |
| `deep` | Full session | Comprehensive analysis, follow every thread, learn from developer emails |

**Default is `standard`** - balance between quick reporting and meaningful investigation.

### Quick Mode
- Read emails, generate MCP reports, send summary email
- Archive processed emails
- No investigation follow-up

### Standard Mode (Default)
- Everything in Quick, plus:
- Investigate NEW failures (not in yesterday's data)
- Git blame/log to identify commits that may have caused regressions
- Check if known issues have fixes pending
- Update TODO with improvement ideas
- Write execution log

### Deep Mode
- Everything in Standard, plus:
- Review ALL failures, not just new ones
- Historical regression analysis (when did each failing test start failing?)
- Read and learn from forwarded developer emails
- Cross-reference exception patterns with code changes
- Propose actionable fixes, not just observations
- Comprehensive execution log with reasoning

---

## Data Sources

This report uses **two complementary data sources**:

### 1. Inbox Emails (Primary for Summary Statistics)

| Email Type | Subject Pattern | Content |
|------------|-----------------|---------|
| TestResults | `TestResults MM/DD - MM/DD ...` | Nightly test summary (8:00 AM) |
| Hang Alert | `[COMPUTER (branch)] !!! TestResults alert` | Log frozen >1 hour |
| Exceptions | `New posts to /home/issues/exceptions` | Exception digest (12:00 AM) |
| Support | Contains support board references | Support digest (if activity) |

### 2. LabKey MCP (Detailed Drill-Down)

| Tool | Purpose |
|------|---------|
| `get_daily_test_summary()` | Detailed per-run data |
| `save_exceptions_report()` | Full stack traces |
| `get_support_summary()` | Support thread details |

---

## Data Validation Requirements

**This report MUST fail if required data cannot be obtained. NEVER substitute stale data.**

### Required Data (MUST have - fail if missing)

| Data Source | Required? | Failure Action |
|-------------|-----------|----------------|
| Nightly test data (MCP) | **YES** | FAIL the report - do not send email |
| Fresh MCP query results | **YES** | FAIL - never use cached files from prior days |

### Optional Data (zero is valid)

| Data Source | Zero Valid? | Notes |
|-------------|-------------|-------|
| Exceptions | Yes (rare) | No exceptions some days is possible |
| Support threads | Yes (common) | No new threads is normal |

### Validation Rules

1. **Nightly tests**: The MCP call `get_daily_test_summary()` MUST succeed and return runs for today's date
   - If MCP call fails: FAIL the report
   - If MCP returns zero runs for today: FAIL (indicates either no tests ran or MCP permission issues)

2. **Never use stale data**: If you cannot query fresh data from MCP:
   - Do NOT fall back to reading old `nightly-report-*.md` files from prior days
   - Do NOT use `daily-summary-*.json` files as the primary data source
   - These files are for historical comparison ONLY, not substitutes for today's data

3. **Failure notification**: If the report cannot complete due to missing data:
   - Send an ERROR email with subject: `[ERROR] Skyline Daily Summary - Month DD, YYYY - Data Unavailable`
   - Body should explain: which data source failed, likely cause (MCP permissions?), how to fix
   - Exit with non-zero status so the scheduled task shows as failed

---

## Default Date Logic

Each report type has different day boundaries:

| Report | Window | Default Date |
|--------|--------|--------------|
| Nightly | 8:01 AM to 8:00 AM next day | Today (if after 8 AM) or yesterday |
| Exceptions | 12:00 AM to 11:59 PM | Yesterday (complete 24h) |
| Support | Last N days | 1 day |

---

## Step-by-Step Instructions

### Step 1: Determine Dates

If user provided a date argument, use it for all reports.

If no date provided, calculate defaults:
- For nightly: Current time before 8 AM -> yesterday's date; after 8 AM -> today's date
- For exceptions: Yesterday's date
- For support: 1 day lookback

### Step 2: Read Inbox Emails

Search the Gmail inbox for today's notification emails:

```
search_emails(query="in:inbox from:skyline@proteinms.net newer_than:2d")
```

For each email found, use `read_email(messageId)` to get full content.

**From TestResults email, extract:**
- Subject line summary: Err/Warn/Pass/Missing counts, total tests
- Per-computer table with: Computer, Memory, Tests, PostTime, Duration, Failures, Leaks, Git hash
- Failure/Leak/Hang matrix showing which tests failed on which computers
- "(hang)" notation in Duration column
- Missing computers list

**Color coding meanings** (see ai/docs/mcp/nightly-tests.md for details):
- Green (#caff95): All metrics normal
- Yellow (#ffffca): 3-4 SDs from trained mean
- Red (#ffcaca): Failures/leaks/hangs OR >4 SDs from mean
- Gray (#cccccc): Unexpected computer reported

**From Hang alert emails, extract:**
- Computer name and branch from subject
- Timestamp of the alert
- End of log showing last test before hang

**From Exceptions email, extract:**
- Each exception entry with location, version, Installation ID, timestamp, stack trace
- Group by Installation ID (same user hitting repeatedly vs different users)

### Step 3: Generate MCP Reports

Run these three MCP calls:

```
get_daily_test_summary(report_date="YYYY-MM-DD")
save_exceptions_report(report_date="YYYY-MM-DD")
get_support_summary(days=1)
```

**Validate each call succeeded** - see Data Validation Requirements above.

Read generated reports from `ai/.tmp/`:
- `nightly-report-YYYYMMDD.md`
- `exceptions-report-YYYYMMDD.md`
- `support-report-YYYYMMDD.md`

### Step 4: Fetch Failure Details and Fingerprints

For each test failure, fetch detailed stack trace:

```
get_run_failures(run_id, container_path)
```

Extract: exception type, message, location, fingerprint.

**Build failure URLs** using pattern:
```
https://skyline.ms/home/development/{folder}/testresults-showFailures.view?end={MM}%2F{DD}%2F{YYYY}&failedTest={TestName}
```

### Step 5: Check Computer Status and Alarms

```
check_computer_alarms()
```

Returns due/overdue alarms for computers that should be reactivated.

For missing computers, check status:
```
list_computer_status(container_path="/home/development/Nightly x64")
```

### Step 6: Analyze Patterns

**First, check release cycle context** by reading `ai/docs/release-cycle-guide.md`:
- If FEATURE COMPLETE, both master and release branch run nightly tests
- Early after branch creation, same failure on both = single issue, not "systemic"

Use the pattern analysis tool:

```
analyze_daily_patterns(report_date="YYYY-MM-DD", days_back=7)
```

This detects:
- **NEW**: First time ever in history
- **SYSTEMIC**: Affects 3+ machines
- **RECURRING**: Seen before, returned after absence
- **REGRESSION**: Failing again after recorded fix
- **CHRONIC**: Intermittent spanning 30+ days
- **EXTERNAL**: Involves external service (Koina, Panorama)

### Step 7: Search PRs for Potential Fixes (Standard/Deep Mode)

For tests marked "Check PRs" (multi-day failure that stopped):

1. Query test history: `query_test_history(test_name="TestName")`
2. Search merged PRs: `gh pr list --state merged --search "TestName" --limit 5`
3. Classify: "Likely fixed by PR#XXXX" or "No fix PR found - may be intermittent"

### Step 8: Check for Already-Fixed Failures

Compare commit hashes from failing runs with current branch HEAD. A failure may be a "stale echo" from an older commit.

If a fix PR merged after the failing run's commit:
- Note: "Already fixed by PR#XXXX, expect resolution tomorrow"
- Skip further investigation

### Step 9: Investigate True Regressions (Standard/Deep Mode)

For failures running on current HEAD:

1. Find test file with grep
2. Git blame recent commits to related files
3. Cross-reference failure start date with commit dates
4. Document: "TestFoo likely regressed by commit abc123"

### Step 10: Save Daily Summary JSON

```
save_daily_summary(
    report_date="YYYY-MM-DD",
    nightly_summary='{"errors": N, "warnings": N, "passed": N, "missing": N, "total_tests": N}',
    nightly_failures='{"TestName": {"computers": ["COMPUTER1"], "fingerprint": "abc123...", "exception_type": "...", "exception_brief": "...", "location": "File.cs:123"}}',
    nightly_leaks='{"TestName": ["COMPUTER1"]}',
    nightly_hangs='{"TestName": ["COMPUTER1"]}',
    missing_computers='["COMPUTER1", "COMPUTER2"]',
    exception_count=N,
    exception_signatures='{"ExceptionType at File.cs:line": {"count": N, "installation_ids": ["id1"]}}',
    support_threads=N
)
```

### Step 11: Archive Processed Emails

```
batch_modify_emails(messageIds=[...], removeLabelIds=["INBOX"])
```

### Step 12: Self-Improvement Reflection (Required)

1. Read active TODO at `ai/todos/active/TODO-20251228_daily_report_improvements.md`
2. Vote on 1-3 existing backlog items based on this session's experience
3. Record observations and new improvement ideas

### Step 13: Write Execution Log

Save to `ai/.tmp/logs/daily-session-YYYYMMDD.md`:
- Effort level, duration, timestamps
- Data sources consulted
- Key observations
- Investigations performed
- Conclusions
- System improvement feedback (votes and new ideas)

---

## Email Format

**Use HTML formatting, not Markdown.** Gmail does not render Markdown.

```
draft_email(
    ...
    body="Plain text fallback",
    htmlBody="<p>HTML formatted version</p>",
    mimeType="multipart/alternative"
)
```

**Subject**: `Skyline Daily Summary - Month DD, YYYY`

**Structure**:
```
## Quick Status
- Nightly: [Err: X | Warn: X | Pass: X | Crashed: X | Missing: X] - N tests
- Exceptions: X new (Y unique issues, Z users affected)
- Support: X threads needing attention

## Key Findings
[Prioritized list - early terminations are HIGH PRIORITY]

## Already Fixed (Stale Echoes)
[Failures from old commits that have been fixed]

## Details
[Expandable sections for each category]

## Improvement Ideas
[Votes and new ideas from self-reflection]
```

### Detecting Early Terminations

Runs that terminate before their expected duration indicate crashes or infrastructure problems. The MCP report flags these in the Anomaly column as `short (N min)`.

**Normal duration**: 540 minutes (9 hours) for most computers.

**Flag as "Crashed"**: Any run with `short` in the Anomaly column. Include in Key Findings:

```html
<h3>Runs Terminated Early (Crashed)</h3>
<ul>
  <li><strong>COMPUTER</strong> - 80 min (expected 540) - investigate crash cause</li>
</ul>
```

**Priority**: Early terminations are HIGH priority - they may indicate:
- Test runner crash
- Machine reboot/shutdown
- Infrastructure failure
- Unhandled exception in test harness

### Investigating Early Terminations

For each crashed run, pull the log and analyze:

```
# 1. Find run IDs for crashed runs
query_test_runs(container_path="/home/development/Nightly x64", days=2)
# Look for runs with Duration < 540

# 2. Pull testrunner section for crashed run
save_run_log(run_id=XXXXX, part="testrunner")
# Saves to ai/.tmp/testrun-log-XXXXX-testrunner.txt

# 3. View end to see crash context (not failure summaries)
tail -50 ai/.tmp/testrun-log-XXXXX-testrunner.txt
```

**What to look for in the log tail**:
- `# Process TestRunner had nonzero exit code -1073741819` = ACCESS_VIOLATION (native crash)
- `Unhandled Exception:` followed by stack trace = .NET crash with diagnostics
- Last test name before crash = which test was running when it died
- No exception = sudden termination (machine reboot, kill, power loss)

**Common exit codes**:
| Exit Code | Meaning |
|-----------|---------|
| -1073741819 | ACCESS_VIOLATION (0xC0000005) - native memory corruption |
| -1073740791 | Stack overflow |
| -1 | General failure |

**Pattern analysis**: Compare crashed runs for common factors:
- Same machine? → Machine-specific issue (hardware, drivers, configuration)
- Same test? → Test bug causing crash
- Same toolchain? → Compiler/runtime issue (e.g., VS 2026 vs VS 2022)
- Same time of day? → Scheduled task interference

Document findings in `ai/.tmp/suggested-actions-YYYYMMDD.md` under "Infrastructure Issues".

**Test failure format** (HTML):
```html
<li><strong>TestName</strong> - COMPUTER
  <br><code>Exception message</code>
  <br>at File.cs:line
  <br><a href="...">View full stack trace</a>
</li>
```

---

## Output Files

| Location | File | Purpose |
|----------|------|---------|
| `ai/.tmp/` | `nightly-report-YYYYMMDD.md` | Full test results |
| `ai/.tmp/` | `exceptions-report-YYYYMMDD.md` | Exception details |
| `ai/.tmp/` | `support-report-YYYYMMDD.md` | Support summary |
| `ai/.tmp/history/` | `daily-summary-YYYYMMDD.json` | Structured data for trends |
| `ai/.tmp/logs/` | `daily-session-YYYYMMDD.md` | Execution log |
| `ai/.tmp/` | `suggested-actions-YYYYMMDD.md` | Pending actions for review |

---

## Post-Email Exploration Phase

**IMPORTANT**: The email is the critical checkpoint. Send it BEFORE starting exploration.

After the email is sent and execution log written, the scheduled session should continue with deeper investigation until it runs out of ideas or hits the session limit.

**IMPORTANT**: Write findings to `ai/.tmp/suggested-actions-YYYYMMDD.md` progressively after each investigation. The session may be terminated without warning at any turn limit, so never accumulate findings in memory to write at the end.

### Exploration Workflow

```
1. EMAIL SENT (checkpoint - session can safely end after this)
2. Begin exploration phase:
   a. For each exception fingerprint:
      - Check version distribution (only old versions?)
      - Search for PRs touching the file/method since that version
      - If potential fix found, add to suggested-actions.md
   b. For each NEW or SYSTEMIC test failure:
      - Query test history for failure start date
      - Search git log for commits around that date
      - Search merged PRs for test name or related code
      - If correlation found, add to suggested-actions.md
   c. For each "stopped failing" test:
      - Search for merged PRs mentioning the test
      - Add finding to suggested-actions.md
3. Write findings to ai/.tmp/suggested-actions-YYYYMMDD.md
4. Session ends (limit or complete)
```

### Suggested Actions File Format

```markdown
# Suggested Actions - YYYY-MM-DD

## Exception Fixes to Record

### 1. [Exception Name]
**Fingerprint**: `abc123...`
**Evidence**: [Why we think it's fixed]
**Action**: `record_exception_fix(fingerprint="...", pr_number=XXXX)`

## GitHub Issues to Create

### 1. [Test Name] Failure
**Evidence**: [Correlation with PR, timing, etc.]
**Draft issue body**: [Pre-written issue text]

## Tests to Monitor
[Low-priority items to watch]
```

### User Review Workflow

When the user runs `/resume` on the scheduled session:
1. Claude reads `ai/.tmp/suggested-actions-YYYYMMDD.md`
2. Presents each suggested action for approval
3. User can approve, modify, or skip each action
4. Approved actions are executed (record_exception_fix, gh issue create, etc.)

**Key principle**: The automated session does research and prepares actions. The human reviews and approves before any permanent changes.

---

## Follow-up Investigation Tools

| Tool | Purpose |
|------|---------|
| `analyze_daily_patterns(report_date)` | Compare with history |
| `query_test_history(test_name)` | All failures/leaks for a test |
| `record_test_fix(test_name, fix_type, pr_number)` | Record fix for regression detection |
| `list_computer_status(container_path)` | Active/inactive computers |
| `check_computer_alarms()` | Due reactivation reminders |
| `save_test_failure_history(test_name, start_date)` | Historical failures |
| `save_run_log(run_id, part)` | Test log by section (full/git/build/testrunner/failures) |
| `get_exception_details(exception_id)` | Exception stack trace |
| `get_support_thread(thread_id)` | Full support thread |
| `save_run_comparison(run_id_before, run_id_after)` | Compare test timing |

### Investigating Test Count Drops

When test count drops significantly (especially Performance Tests):

```
save_run_comparison(
    run_id_before=79482,  # Baseline
    run_id_after=79497,   # Changed
    container_path="/home/development/Performance Tests"
)
```

Review `ai/.tmp/run-comparison-{before}-{after}.md` for:
- NEW/REMOVED tests
- SLOWDOWNS (>50% slower)
- Top impacts on time

---

## Scheduled Session Configuration

**Prompt**: `/pw-daily standard` or `/pw-daily deep`

**Session budget**:
- Quick: ~$0.10-0.20
- Standard: ~$0.50-2.00
- Deep: ~$5.00-15.00

**Default recipient**: brendanx@proteinms.net

---

## Related

- [mcp/nightly-tests.md](mcp/nightly-tests.md) - Nightly test MCP tools
- [mcp/exceptions.md](mcp/exceptions.md) - Exception MCP tools
- [mcp/support.md](mcp/support.md) - Support board MCP tools
- [release-cycle-guide.md](release-cycle-guide.md) - Release phase context
