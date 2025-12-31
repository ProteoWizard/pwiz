# Daily Report System Improvements

**Branch**: ai-context
**Created**: 2025-12-28
**Status**: Active

## Context

This TODO captures improvements to the scheduled daily analysis system (`/pw-daily`). The system generates daily reports on nightly tests, exceptions, and support board activity.

## Completed (2025-12-28)

- [x] Added inbox emails as primary data source (nightly, exceptions, hang alerts)
- [x] Documented email color coding (green/yellow/red/gray) in nightly-tests.md
- [x] Added email archiving step to keep inbox clean
- [x] Added self-improvement reflection step
- [x] Historical JSON storage for trend analysis (Step 6, 8 in /pw-daily)

## Completed (2025-12-30)

- [x] Pattern detection MCP tools (`analyze_daily_patterns`, `save_daily_summary`)
- [x] Prioritized Action Items output (SYSTEMIC, NEW, EXTERNAL, MISSING, RESOLVED)
- [x] Updated `/pw-daily` Steps 6 and 8 to use new MCP tools

## Completed (2025-12-29)

- [x] Added `fetch_labkey_page` MCP tool for authenticated web page access
- [x] Effort level modes (quick/standard/deep) for controlling session depth
- [x] Execution logging (Step 13) - writes to `ai/.tmp/logs/daily-session-YYYYMMDD.md`
- [x] Developer email learning loop (Step 11) - analyzes forwarded developer emails as training signal
- [x] Regression investigation step (Step 10) - git blame for new failures

## Backlog

### Tier 1: High Value, Achievable Now

#### 1. ~~Level 1 Automation - Pattern Detection~~ ✅ COMPLETED 2025-12-30
**Problem**: Reports show data but don't highlight patterns requiring immediate attention
**Solution**: Add pattern detection to daily report:
- Detect NEW failures (not in yesterday's run)
- Detect ALL-MACHINES-AFFECTED pattern → flag for immediate attention
- Flag tests involving known external services (Koina, Panorama)
- Track "expected fixes" and verify next day
**Implementation**: Created `patterns.py` MCP module with `analyze_daily_patterns` and `save_daily_summary` tools

#### 2. Parse Installation ID from Exceptions
**Problem**: Cannot distinguish "1 user hit this 4 times" from "4 users hit this once each"
**Solution**: Parse Installation ID from exception email HTML
**Implementation**: Regex for `Installation ID: XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX`
**Value**: Prioritize bugs blocking multiple users vs one-off issues

### Tier 2: High Value, Requires C# Code Changes

#### 3. Automated Stack Trace Capture for Hangs
**Problem**: Hang alerts notify developers to RDP and attach debuggers, but developers rarely catch it in time
**Solution**: Have SkylineNightly automatically capture thread/stack traces from hung TestRunner.exe
**Implementation options**:
- ProcDump.exe (SysInternals) to capture minidump
- ClrMD (Microsoft.Diagnostics.Runtime) for managed stack traces
- Post extracted traces to LabKey
**Value**: Transforms hang alerts from "notification to act" to "automated diagnosis"
**Files**: SkylineNightly project

#### 4. TestRunner Flush Logging for Hung Test Identification
**Problem**: Hang alert emails show last *completed* test, not the *hung* test (log only flushes on line completion)
**Solution**: Flush test name immediately when starting:
```
[Flush] "Starting: TestFoo"
... test runs ...
[Flush] "Completed: TestFoo (passed, 45s)"
```
**Files**: TestRunner.exe output code
**Value**: Know which test is hung immediately, not after 8 AM report

#### 5. Historical Regression Detection
**Problem**: Intermittent failures can persist for months before someone traces when they started (e.g., June bug found in October)
**Solution**: For each failing test, query past 30/90/365 days to find introduction date
**Output format**:
```
TestFoo - INTERMITTENT REGRESSION DETECTED
- First failure: 2025-06-15 (after commit abc123)
- Failure rate: 12% (47 failures in 390 runs since June)
- Before June: 0 failures in 180 runs
- Recommendation: Investigate commit abc123
```
**Value**: Could have caught June→October bug months earlier

### Tier 3: Medium Value

#### 6. Track Recurring Missing Computers
**Problem**: Same computers missing for multiple days with no investigation
**Solution**: Track "days missing" and escalate after threshold (e.g., 3 days)
**Depends on**: Historical JSON (now available)

#### 7. Date Boundary Verification
**Problem**: Occasional mismatch between email data and MCP data due to different time windows
**Solution**: Log explicit date ranges from each source, flag discrepancies
**Value**: Accuracy and trust in reports

#### 8. Git Integration for Developer Attribution
**Problem**: Reports show failures but don't identify who should investigate
**Solution**:
- Get commit author from git hash: `git show --format='%an <%ae>' -s <hash>`
- Map stack trace files to code owners
- Suggest "@developer should investigate"
**Value**: Actionable recommendations, not just data

### Tier 4: Low Priority / Quick Fixes

#### 9. Verify Leak Type Distinction
**Problem**: Reports say "leak" but email distinguishes "Handle leak" vs "Memory and handle leak"
**Solution**: Verify `leak_type` from `leaks_by_date` query is surfaced in reports
**Status**: Data already available, just needs verification

#### 10. Standardize Email Subject Format
**Problem**: Subject lines vary slightly ("Daily Summary" vs "Daily Report")
**Solution**: Use consistent format: `Skyline Daily Summary - Month DD, YYYY`
**Priority**: Cosmetic

## Future Ideas (Not Currently Planned)

### Browser Automation for Visual Analysis
Claude Code can analyze images but cannot acquire them autonomously. A browser MCP like `superpowers-chrome` would enable:
- Navigate to skyline.ms test history pages
- Capture failure history graphs
- Autonomous visual regression detection

**Status**: Requires external dependency, deferred

### Investigation Patterns Reference
The following patterns from developer reports represent ideal automated investigation:

| Pattern | Trigger | Automation Feasibility |
|---------|---------|------------------------|
| New widespread failure | Test failing on all machines | High - compare with yesterday |
| Intermittent regression | Test failing periodically | High - query historical data |
| Cascading leak effect | All machines show same leak | High - detect unanimous pattern |
| External service issue | Koina/Panorama tests fail | Medium - known service list |
| Fix verification | Expected fix committed | High - track pending fixes |

## Progress Log

### 2025-12-30
- Implemented Level 1 Automation - Pattern Detection:
  - Created `ai/mcp/LabKeyMcp/tools/patterns.py` with two new MCP tools:
    - `analyze_daily_patterns(report_date, days_back)` - Compares today vs history, returns prioritized Action Items
    - `save_daily_summary(...)` - Saves structured JSON for historical comparison
  - Pattern detection includes:
    - 🔴 SYSTEMIC: Issues affecting 3+ machines (code bug, not environment)
    - 🆕 NEW: First appearance since yesterday (regression)
    - 🌐 EXTERNAL: Tests involving Koina, Panorama, UniProt, Prosit
    - ⚠️ MISSING N DAYS: Computers absent for consecutive days
    - ✅ RESOLVED: Issues fixed since yesterday
  - Updated `/pw-daily` command (Steps 6 and 8) to use new MCP tools
  - Registered patterns module in tools/__init__.py
  - Added new tools to reporting machine's `.claude/settings.local.json`

- **Investigation needed**: `--allowedTools` vs `settings.local.json` interaction
  - The script `Invoke-DailyReport.ps1` passes `--allowedTools` with wildcards (e.g., `mcp__labkey__*`)
  - Wildcards don't work - we've confirmed tools must be listed explicitly
  - Currently, permissions work via `settings.local.json` on the reporting machine
  - **Question**: Does `--allowedTools` do anything useful? Options:
    1. `--allowedTools` alone is sufficient (no settings.local.json needed)
    2. Both are required (settings.local.json grants permission, --allowedTools filters)
    3. `--allowedTools` is ignored in `-p` mode, only settings.local.json matters
  - **Test plan**: After tomorrow's successful report with settings.local.json, try:
    - Remove a tool from settings.local.json but keep it in --allowedTools
    - If it still works → --allowedTools is sufficient
    - If it fails → settings.local.json is required
  - **If --allowedTools works**: Update script to use explicit tool list instead of wildcards
  - **If settings.local.json required**: Document this clearly, possibly remove --allowedTools from script

### 2025-12-29
- Added `fetch_labkey_page` MCP tool - enables fetching authenticated LabKey pages (failure details, stack traces)
- Implemented effort level modes in `/pw-daily`:
  - `quick`: 1-2 min, report only
  - `standard`: 15-30 min, investigate new failures, git blame (new default)
  - `deep`: Full session, comprehensive analysis, learn from developer emails
- Added execution logging (Step 13) - system now records what it analyzed and decided
- Added developer email learning loop (Step 11) - forwarded developer analyses become training signal
- Added regression investigation step (Step 10) - git blame/log to trace failures to commits
- Key insight: Developer email forwarding creates a feedback loop for continuous improvement

### 2025-12-28
- Created TODO from collaborative analysis session
- Implemented: email data sources, color coding docs, archiving, self-improvement step
- Analyzed 9 real developer report examples to identify valuable patterns
- Proved visual analysis capability (can analyze images, cannot acquire them)
- Implemented Historical JSON storage (Steps 6 and 8 in /pw-daily)
  - Schema: date, nightly summary/failures/leaks/hangs/missing, exceptions by signature, support
  - Files saved to `ai/.tmp/history/daily-summary-YYYYMMDD.json`
  - Enables trend analysis across days/weeks/months
